using Npgsql;
using NpgsqlTypes;
using VehicleTracker.Application.Common.Interfaces;

namespace VehicleTracker.Infrastructure.Persistence;

public sealed class TimescaleRetentionService : IVehicleRetentionService
{
    private const int CommandTimeoutSeconds = 30;

    // TimescaleDB'nin drop_chunks fonksiyonu satır silmez; süresi dolan fiziksel 
    // chunk tablolarını işletim sistemi düzeyinde drop eder. Lock ve bloat oluşmaz.
    private const string DropChunksSql = """
        SELECT count(*) 
        FROM drop_chunks('vehicle_positions', older_than => @cutoff);
        """;

    private readonly NpgsqlDataSource _dataSource;

    public TimescaleRetentionService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<int> DropStaleChunksAsync(
        TimeSpan retentionWindow, 
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow - retentionWindow;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(DropChunksSql, connection)
        {
            CommandTimeout = CommandTimeoutSeconds
        };

        command.Parameters.Add(new NpgsqlParameter("cutoff", NpgsqlDbType.TimestampTz)
        {
            Value = cutoff
        });

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }
}