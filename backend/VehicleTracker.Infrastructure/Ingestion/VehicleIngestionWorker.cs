using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Application.Ingestion;

namespace VehicleTracker.Infrastructure.Ingestion;

public sealed class VehicleIngestionWorker : BackgroundService
{
    private readonly IVehicleFeedService _feedService;
    private readonly IVehiclePositionWriter _writer;
    private readonly IVehicleLiveStateStore _liveStore;
    private readonly ISystemMetricsService _metrics;
    private readonly IVehiclePublisher _publisher;
    private readonly IngestionOptions _options;
    private readonly ILogger<VehicleIngestionWorker> _logger;
    private readonly SnapshotChangeDetector _detector = new();

    public VehicleIngestionWorker(
        IVehicleFeedService feedService,
        IVehiclePositionWriter writer,
        IVehicleLiveStateStore liveStore,
        ISystemMetricsService metrics,
        IVehiclePublisher publisher,
        IOptions<IngestionOptions> options,
        ILogger<VehicleIngestionWorker> logger)
    {
        _feedService = feedService;
        _writer = writer;
        _liveStore = liveStore;
        _metrics = metrics;
        _publisher = publisher;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "VehicleIngestionWorker baslatildi. Polling: {Interval}sn, Saklama: {Retention}saat",
            _options.PollInterval.TotalSeconds,
            _options.Retention.TotalHours);

        using var timer = new PeriodicTimer(_options.PollInterval);

        try
        {
            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                await IngestOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("VehicleIngestionWorker durduruluyor.");
        }
    }

    private async Task IngestOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var feed = await _feedService.FetchLatestPositionsAsync(cancellationToken);
            var detection = _detector.Detect(feed, _options.Retention);

            if (detection.Disposition == SnapshotDisposition.DuplicatePayload)
            {
                return;
            }

            if (detection.Accepted.Count == 0)
            {
                // DB yazımı gerekmese bile bu SHA'yı gördüğümüzü onaylıyoruz
                _detector.Acknowledge(detection.Accepted, feed.PayloadSha256);
                _metrics.RecordSuccessfulPoll(0);
                
                _logger.LogInformation(
                    "Poll tamamlandi: Toplam={Total}, Yeni=0, 3h Eski={Stale}, Sure={Ms}ms",
                    feed.Records.Count,
                    detection.SkippedOutsideRetention,
                    (int)feed.Duration.TotalMilliseconds);
                return;
            }

            var inserted = await _writer.WriteAsync(detection.Accepted, cancellationToken);
            
            // DB yazımı BAŞARILI olduktan sonra onaylıyoruz 
            _detector.Acknowledge(detection.Accepted, feed.PayloadSha256);
            
            _liveStore.Update(detection.Accepted);
            await _publisher.EnqueueUpdatesAsync(detection.Accepted, cancellationToken);

            _metrics.RecordSuccessfulPoll(inserted);

            _logger.LogInformation(
                "Ingestion: Toplam={Total} | Yazilan={Inserted} | 3h Eski={Stale} | Gecersiz={Invalid} | Sure={Ms}ms",
                feed.Records.Count,
                inserted,
                detection.SkippedOutsideRetention,
                feed.TotalSkipped, 
                (int)feed.Duration.TotalMilliseconds);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _metrics.RecordFailedPoll();
            _logger.LogWarning(
                "Ingestion dongusunde gecici hata (sonraki turda tekrar denenecek): {Message}",
                ex.Message);
        }
    }
}