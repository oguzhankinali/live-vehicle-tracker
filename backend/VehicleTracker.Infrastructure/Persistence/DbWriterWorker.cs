using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Domain.Entities;

namespace VehicleTracker.Infrastructure.Persistence;

public sealed class DbWriterWorker : BackgroundService
{
    private static readonly TimeSpan DrainTimeout =
        TimeSpan.FromSeconds(25);

    private readonly IVehicleWriteQueueReader _queue;
    private readonly IVehiclePositionWriter _writer;
    private readonly ISystemMetricsService _metrics;
    private readonly ILogger<DbWriterWorker> _logger;

    public DbWriterWorker(
        IVehicleWriteQueueReader queue,
        IVehiclePositionWriter writer,
        ISystemMetricsService metrics,
        ILogger<DbWriterWorker> logger)
    {
        _queue = queue;
        _writer = writer;
        _metrics = metrics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "DbWriterWorker baslatildi. DB yazimlari arka planda islenecek.");

        using var drainCts = new CancellationTokenSource();

        using var registration = stoppingToken.Register(
            () => drainCts.CancelAfter(DrainTimeout));

        var activeBatchCount = 0;
        var permanentlyRejectedRecords = 0L;

        try
        {
            await foreach (var batch in _queue.ReadAllAsync(drainCts.Token))
            {
                activeBatchCount = batch.Count;

                permanentlyRejectedRecords += await WriteWithRetryAsync(
                    batch,
                    drainCts.Token);

                activeBatchCount = 0;
            }

            _logger.LogInformation(
                "DB kuyrugu bosaltildi. BekleyenBatch={Pending}, " +
                "KaliciReddedilenKayit={Rejected}",
                _queue.PendingBatchCount,
                permanentlyRejectedRecords);
        }
        catch (OperationCanceledException)
            when (drainCts.IsCancellationRequested)
        {
            _logger.LogError(
                "DB yazicisi zaman asimina ugradi. " +
                "BekleyenBatch={Pending}, AktifBatchKayit={Active}, " +
                "KaliciReddedilenKayit={Rejected}. " +
                "Kalici yazilmamis veriler kaybolabilir.",
                _queue.PendingBatchCount,
                activeBatchCount,
                permanentlyRejectedRecords);
        }
    }

    private async Task<long> WriteWithRetryAsync(
        IReadOnlyList<VehiclePositionRecord> batch,
        CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromSeconds(1);

        while (true)
        {
            try
            {
                var inserted = await _writer.WriteAsync(
                    batch,
                    cancellationToken);

                _metrics.RecordRecordsWritten(inserted);

                return 0;
            }
            catch (PostgresException exception)
                when (exception.SqlState.StartsWith(
                          "22", StringComparison.Ordinal) ||
                      exception.SqlState.StartsWith(
                          "23", StringComparison.Ordinal))
            {
                if (batch.Count == 1)
                {
                    _logger.LogError(
                        exception,
                        "Kalici DB hatasi: kayit yazilamadi ve atlandi. " +
                        "EntityId={EntityId}, SqlState={SqlState}",
                        batch[0].FeedEntityId,
                        exception.SqlState);

                    return 1;
                }

                var middle = batch.Count / 2;

                var firstRejected = await WriteWithRetryAsync(
                    batch.Take(middle).ToList(),
                    cancellationToken);

                var secondRejected = await WriteWithRetryAsync(
                    batch.Skip(middle).ToList(),
                    cancellationToken);

                return firstRejected + secondRejected;
            }
            catch (Exception exception)
                when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(
                    exception,
                    "DB yazimi basarisiz. {Delay} saniye sonra " +
                    "yeniden denenecek. KayitSayisi={Count}",
                    delay.TotalSeconds,
                    batch.Count);

                await Task.Delay(delay, cancellationToken);

                delay = TimeSpan.FromSeconds(
                    Math.Min(delay.TotalSeconds * 2, 30));
            }
        }
    }
}
