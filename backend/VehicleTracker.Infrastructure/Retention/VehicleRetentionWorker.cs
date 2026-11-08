using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Application.Ingestion;

namespace VehicleTracker.Infrastructure.Retention;

public sealed class VehicleRetentionWorker : BackgroundService
{
    private readonly IVehicleRetentionService _retentionService;
    private readonly IVehicleLiveStateStore _liveStore;
    private readonly IngestionOptions _options;
    private readonly ILogger<VehicleRetentionWorker> _logger;

    public VehicleRetentionWorker(
        IVehicleRetentionService retentionService,
        IVehicleLiveStateStore liveStore,
        IOptions<IngestionOptions> options,
        ILogger<VehicleRetentionWorker> logger)
    {
        _retentionService = retentionService;
        _liveStore = liveStore;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "VehicleRetentionWorker baslatildi. Kontrol periyodu: {Interval}sn, Saklama penceresi: {Retention}saat",
            _options.CleanupInterval.TotalSeconds,
            _options.Retention.TotalHours);

        using var timer = new PeriodicTimer(_options.CleanupInterval);

        try
        {
            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                await CleanupOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("VehicleRetentionWorker durduruluyor.");
        }
    }

    private async Task CleanupOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var thresholdUtc = DateTime.UtcNow - _options.Retention;

            // 1. RAM'deki eski araçları temizle
            _liveStore.PruneStaleVehicles(thresholdUtc);

            // 2. Veritabanındaki eski chunk'ları temizle
            var droppedCount = await _retentionService.DropStaleChunksAsync(_options.Retention, cancellationToken);

            if (droppedCount > 0)
            {
                _logger.LogInformation(
                    "Retention: {Count} adet suresi dolmus hypertable chunk'i fiziksel olarak drop edildi.",
                    droppedCount);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Retention temizleme dongusunde gecici hata (sonraki periyotta tekrar denenecek): {Message}",
                ex.Message);
        }
    }
}