using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Application.Common.Models;
using VehicleTracker.Application.Ingestion;
using VehicleTracker.Domain.Entities;
using VehicleTracker.Domain.Enums;

namespace VehicleTracker.Infrastructure.Persistence;

public sealed class TimescaleVehicleHistoryService : IVehicleHistoryService
{
    private const int CommandTimeoutSeconds = 15;
    private readonly NpgsqlDataSource _dataSource;
    private readonly IngestionOptions _options;

    private const string SelectHistorySql = """
        SELECT feed_entity_id, observed_at, vehicle_id, vehicle_label, trip_id, route_id,
               direction_id, latitude, longitude, bearing, speed, stop_id,
               current_stop_sequence, current_status, occupancy_status, received_at_utc
        FROM vehicle_positions
        WHERE vehicle_id = @vehicle_id
          AND observed_at >= @from
          AND observed_at <= @to
        ORDER BY observed_at DESC
        LIMIT @limit OFFSET @offset;

        SELECT count(*)
        FROM vehicle_positions
        WHERE vehicle_id = @vehicle_id
          AND observed_at >= @from
          AND observed_at <= @to;
        """;

    public TimescaleVehicleHistoryService(NpgsqlDataSource dataSource, IOptions<IngestionOptions> options)
    {
        _dataSource = dataSource;
        _options = options.Value;
    }

    // API Sözleşmesi: Unspecified gelen tarihler UTC kabul edilir.
    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    public async Task<PagedResult<VehiclePositionRecord>> GetHistoryAsync(
        VehicleHistoryQuery query, 
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 500);
        var offset = (page - 1) * pageSize;

        var now = DateTime.UtcNow;
        var absoluteMinTime = now - _options.Retention;

        var from = query.FromUtc.HasValue ? ToUtc(query.FromUtc.Value) : absoluteMinTime;
        if (from < absoluteMinTime) from = absoluteMinTime;
        if (from > now) from = now;

        var to = query.ToUtc.HasValue ? ToUtc(query.ToUtc.Value) : now;
        if (to > now) to = now;

        if (from > to)
        {
            throw new ArgumentException("Gecersiz tarih araligi: 'from' degeri 'to' degerinden buyuk olamaz.");
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(SelectHistorySql, connection)
        {
            CommandTimeout = CommandTimeoutSeconds
        };

        command.Parameters.Add(new NpgsqlParameter("vehicle_id", NpgsqlDbType.Text) { Value = query.VehicleId });
        command.Parameters.Add(new NpgsqlParameter("from", NpgsqlDbType.TimestampTz) { Value = from });
        command.Parameters.Add(new NpgsqlParameter("to", NpgsqlDbType.TimestampTz) { Value = to });
        command.Parameters.Add(new NpgsqlParameter("limit", NpgsqlDbType.Integer) { Value = pageSize });
        command.Parameters.Add(new NpgsqlParameter("offset", NpgsqlDbType.Integer) { Value = offset });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var items = new List<VehiclePositionRecord>(pageSize);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new VehiclePositionRecord
            {
                FeedEntityId = reader.GetString(0),
                Timestamp = reader.GetDateTime(1),
                VehicleId = reader.GetString(2),
                VehicleLabel = reader.IsDBNull(3) ? null : reader.GetString(3),
                TripId = reader.IsDBNull(4) ? null : reader.GetString(4),
                RouteId = reader.IsDBNull(5) ? null : reader.GetString(5),
                DirectionId = reader.IsDBNull(6) ? null : reader.GetInt64(6),
                Latitude = reader.GetDouble(7),
                Longitude = reader.GetDouble(8),
                Bearing = reader.IsDBNull(9) ? null : reader.GetFloat(9),
                Speed = reader.IsDBNull(10) ? null : reader.GetFloat(10),
                StopId = reader.IsDBNull(11) ? null : reader.GetString(11),
                CurrentStopSequence = reader.IsDBNull(12) ? null : (uint)reader.GetInt64(12),
                CurrentStatus = reader.IsDBNull(13) ? null : (VehicleStopStatus)reader.GetInt16(13),
                OccupancyStatus = reader.IsDBNull(14) ? null : (OccupancyStatus)reader.GetInt16(14),
                ReceivedAtUtc = reader.GetDateTime(15)
            });
        }

        await reader.NextResultAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        var totalCount = Convert.ToInt32(reader.GetInt64(0));

        return new PagedResult<VehiclePositionRecord>(items, totalCount, page, pageSize);
    }
}