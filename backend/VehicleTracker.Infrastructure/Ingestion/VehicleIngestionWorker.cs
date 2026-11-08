using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Application.Ingestion;
using VehicleTracker.Infrastructure.Persistence;

namespace VehicleTracker.Infrastructure.Ingestion;

public sealed class VehicleIngestionWorker : BackgroundService
{
    private readonly IVehicleFeedService _feedService;
    private readonly DbWriteQueue _dbQueue;
    private readonly IVehicleLiveStateStore _liveStore;
    private readonly ISystemMetricsService _metrics;
    private readonly IVehiclePublisher _publisher;
    private readonly IngestionOptions _options;
    private readonly ILogger<VehicleIngestionWorker> _logger;
    private readonly SnapshotChangeDetector _detector = new();

    public VehicleIngestionWorker(
        IVehicleFeedService feedService,
        DbWriteQueue dbQueue,
        IVehicleLiveStateStore liveStore,
        ISystemMetricsService metrics,
        IVehiclePublisher publisher,
        IOptions<IngestionOptions> options,
        ILogger<VehicleIngestionWorker> logger)
    {
        _feedService = feedService;
        _dbQueue = dbQueue;
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
        finally
        {
            _dbQueue.Complete(); // Kapanışta kuyruğa yeni veri gelmeyeceğini bildir
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
                _metrics.RecordSuccessfulPoll(0);
                return;
            }

            if (detection.Accepted.Count == 0)
            {
                _detector.Acknowledge(detection.Accepted, feed.PayloadSha256);
                _metrics.RecordSuccessfulPoll(0);
                
                _logger.LogInformation(
                    "Poll tamamlandi: Toplam={Total}, Yeni=0, 3h Eski={Stale}, Sure={Ms}ms",
                    feed.Records.Count,
                    detection.SkippedOutsideRetention,
                    (int)feed.Duration.TotalMilliseconds);
                return;
            }

            // 1. Canlı RAM'i güncelle (DB'yi beklemeden!)
            _liveStore.Update(detection.Accepted);

            // 2. SignalR üzerinden anında yayınla (DB'yi beklemeden!)
            await _publisher.EnqueueUpdatesAsync(detection.Accepted, cancellationToken);

            // 3. DB'ye yazılması için kuyruğa at (DbWriterWorker arka planda retry ile yazacak)
            await _dbQueue.EnqueueAsync(detection.Accepted, cancellationToken);

            // 4. Onayla ve metrikleri güncelle
            _detector.Acknowledge(detection.Accepted, feed.PayloadSha256);
            
            // DÜZELTME: Artık yazılan kayıt sayısını DbWriterWorker sayıyor.
            // Biz sadece okuma başarılı oldu demek için 0 gönderiyoruz.
            _metrics.RecordSuccessfulPoll(0); 

            _logger.LogInformation(
                "Ingestion: Toplam={Total} | Kuyruga Alinan={Inserted} | 3h Eski={Stale} | Gecersiz={Invalid} | Sure={Ms}ms",
                feed.Records.Count,
                detection.Accepted.Count,
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