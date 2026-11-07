using Npgsql;
using NpgsqlTypes;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Domain.Entities;

namespace VehicleTracker.Infrastructure.Persistence;

public sealed class NpgsqlVehiclePositionWriter : IVehiclePositionWriter
{
    private const int CommandTimeoutSeconds = 15;

    private const string InsertSql = """
        INSERT INTO vehicle_positions (
            feed_entity_id, observed_at, vehicle_id, vehicle_label, trip_id, route_id,
            direction_id, latitude, longitude, bearing, speed, stop_id,
            current_stop_sequence, current_status, occupancy_status, received_at_utc)
        SELECT
            t.feed_entity_id, t.observed_at, t.vehicle_id, t.vehicle_label, t.trip_id, t.route_id,
            t.direction_id, t.latitude, t.longitude, t.bearing, t.speed, t.stop_id,
            t.current_stop_sequence, t.current_status, t.occupancy_status, t.received_at_utc
        FROM unnest(
            @feed_entity_id, @observed_at, @vehicle_id, @vehicle_label, @trip_id, @route_id,
            @direction_id, @latitude, @longitude, @bearing, @speed, @stop_id,
            @current_stop_sequence, @current_status, @occupancy_status, @received_at_utc
        ) AS t(
            feed_entity_id, observed_at, vehicle_id, vehicle_label, trip_id, route_id,
            direction_id, latitude, longitude, bearing, speed, stop_id,
            current_stop_sequence, current_status, occupancy_status, received_at_utc)
        ON CONFLICT (feed_entity_id, observed_at) DO NOTHING
        """;

    private readonly NpgsqlDataSource _dataSource;

    public NpgsqlVehiclePositionWriter(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<int> WriteAsync(
        IReadOnlyList<VehiclePositionRecord> records,
        CancellationToken cancellationToken = default)
    {
        if (records.Count == 0)
        {
            return 0;
        }

        var count = records.Count;

        var feedEntityIds = new string[count];
        var observedAt = new DateTime[count];
        var vehicleIds = new string[count];
        var vehicleLabels = new string?[count];
        var tripIds = new string?[count];
        var routeIds = new string?[count];
        var directionIds = new long?[count];
        var latitudes = new double[count];
        var longitudes = new double[count];
        var bearings = new float?[count];
        var speeds = new float?[count];
        var stopIds = new string?[count];
        var stopSequences = new long?[count];
        var statuses = new short?[count];
        var occupancies = new short?[count];
        var receivedAt = new DateTime[count];

        for (var i = 0; i < count; i++)
        {
            var record = records[i];

            feedEntityIds[i] = record.FeedEntityId;
            observedAt[i] = record.Timestamp;
            vehicleIds[i] = record.VehicleId;
            vehicleLabels[i] = record.VehicleLabel;
            tripIds[i] = record.TripId;
            routeIds[i] = record.RouteId;
            directionIds[i] = record.DirectionId;
            latitudes[i] = record.Latitude;
            longitudes[i] = record.Longitude;
            bearings[i] = record.Bearing;
            speeds[i] = record.Speed;
            stopIds[i] = record.StopId;
            stopSequences[i] = record.CurrentStopSequence.HasValue ? (long)record.CurrentStopSequence.Value : null;
            statuses[i] = (short?)record.CurrentStatus;
            occupancies[i] = (short?)record.OccupancyStatus;
            receivedAt[i] = record.ReceivedAtUtc;
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(InsertSql, connection)
        {
            CommandTimeout = CommandTimeoutSeconds
        };

        command.Parameters.Add(ArrayParam("feed_entity_id", NpgsqlDbType.Text, feedEntityIds));
        command.Parameters.Add(ArrayParam("observed_at", NpgsqlDbType.TimestampTz, observedAt));
        command.Parameters.Add(ArrayParam("vehicle_id", NpgsqlDbType.Text, vehicleIds));
        command.Parameters.Add(ArrayParam("vehicle_label", NpgsqlDbType.Text, vehicleLabels));
        command.Parameters.Add(ArrayParam("trip_id", NpgsqlDbType.Text, tripIds));
        command.Parameters.Add(ArrayParam("route_id", NpgsqlDbType.Text, routeIds));
        command.Parameters.Add(ArrayParam("direction_id", NpgsqlDbType.Bigint, directionIds));
        command.Parameters.Add(ArrayParam("latitude", NpgsqlDbType.Double, latitudes));
        command.Parameters.Add(ArrayParam("longitude", NpgsqlDbType.Double, longitudes));
        command.Parameters.Add(ArrayParam("bearing", NpgsqlDbType.Real, bearings));
        command.Parameters.Add(ArrayParam("speed", NpgsqlDbType.Real, speeds));
        command.Parameters.Add(ArrayParam("stop_id", NpgsqlDbType.Text, stopIds));
        command.Parameters.Add(ArrayParam("current_stop_sequence", NpgsqlDbType.Bigint, stopSequences));
        command.Parameters.Add(ArrayParam("current_status", NpgsqlDbType.Smallint, statuses));
        command.Parameters.Add(ArrayParam("occupancy_status", NpgsqlDbType.Smallint, occupancies));
        command.Parameters.Add(ArrayParam("received_at_utc", NpgsqlDbType.TimestampTz, receivedAt));

        // Eklenen gerçek satır sayısını döner (DO NOTHING ile atlananlar sayılmaz)
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static NpgsqlParameter ArrayParam<T>(string name, NpgsqlDbType elementType, T[] values) =>
        new(name, NpgsqlDbType.Array | elementType) { Value = values };
}