using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Domain.Entities;

namespace VehicleTracker.Infrastructure.Persistence;

public sealed class DbWriterWorker : BackgroundService
{
    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(10);

    private readonly DbWriteQueue _queue;
    private readonly IVehiclePositionWriter _writer;
    private readonly ISystemMetricsService _metrics;
    private readonly ILogger<DbWriterWorker> _logger;

    public DbWriterWorker(
        DbWriteQueue queue,
        IVehiclePositionWriter writer,
        ISystemMetricsService metrics,
        ILogger<DbWriterWorker> logger)
    {
        _queue = queue;
        _writer = writer;
        _metrics = metrics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DbWriterWorker baslatildi. DB yazimlari arka planda islenecek.");

        using var drainCts = new CancellationTokenSource();
        using var registration = stoppingToken.Register(() => drainCts.CancelAfter(DrainTimeout));

        try
        {
            await foreach (var batch in _queue.ReadAllAsync(drainCts.Token))
            {
                await WriteWithRetryAsync(batch, drainCts.Token);
            }

            _logger.LogInformation("DbWriterWorker: kuyruk bosaltildi, durduruluyor.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogError(
                "Kapanis suresi doldu; kuyrukta yazilamayan kayitlar kalmis olabilir (VERI KAYBI).");
        }
    }

    private async Task WriteWithRetryAsync(
        IReadOnlyList<VehiclePositionRecord> batch,
        CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromSeconds(1);

        while (true)
        {
            try
            {
                var inserted = await _writer.WriteAsync(batch, cancellationToken);
                _metrics.RecordRecordsWritten(inserted); // Gerçek yazılanı say
                return;
            }
            catch (PostgresException exception)
                when (exception.SqlState != null && 
                     (exception.SqlState.StartsWith("22", StringComparison.Ordinal) || 
                      exception.SqlState.StartsWith("23", StringComparison.Ordinal)))
            {
                if (batch.Count == 1)
                {
                    _logger.LogError(
                        exception,
                        "Kalici DB hatasi: bozuk kayit atildi (1 kayit). EntityId={EntityId}",
                        batch[0].FeedEntityId);
                    return;
                }

                // Paketi ikiye bölerek sağlam kayıtları kurtar, sadece bozuk olanı izole et
                var mid = batch.Count / 2;
                await WriteWithRetryAsync(batch.Take(mid).ToList(), cancellationToken);
                await WriteWithRetryAsync(batch.Skip(mid).ToList(), cancellationToken);
                return;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(
                    "DB yazimi basarisiz; {Delay}sn sonra tekrar denenecek ({Count} kayit bekliyor): {Message}",
                    delay.TotalSeconds,
                    batch.Count,
                    exception.Message);

                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 30));
            }
        }
    }
}