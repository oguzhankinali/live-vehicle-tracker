using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VehicleTracker.Application.Common.Interfaces;

namespace VehicleTracker.Application.Ingestion;

public sealed class VehicleIngestionService
{
    private readonly IVehicleFeedService _feedService;
    private readonly IVehicleWriteQueue _writeQueue;
    private readonly IVehicleLiveStateStore _liveStore;
    private readonly ISystemMetricsService _metrics;
    private readonly IVehiclePublisher _publisher;
    private readonly IngestionOptions _options;
    private readonly ILogger<VehicleIngestionService> _logger;

    private readonly SnapshotChangeDetector _detector = new();

    public VehicleIngestionService(
        IVehicleFeedService feedService,
        IVehicleWriteQueue writeQueue,
        IVehicleLiveStateStore liveStore,
        ISystemMetricsService metrics,
        IVehiclePublisher publisher,
        IOptions<IngestionOptions> options,
        ILogger<VehicleIngestionService> logger)
    {
        _feedService = feedService;
        _writeQueue = writeQueue;
        _liveStore = liveStore;
        _metrics = metrics;
        _publisher = publisher;
        _options = options.Value;
        _logger = logger;
    }

    public async Task IngestOnceAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var feed = await _feedService.FetchLatestPositionsAsync(
                cancellationToken);

            var detection = _detector.Detect(
                feed,
                _options.Retention);

            if (detection.Disposition == SnapshotDisposition.DuplicatePayload)
            {
                _metrics.RecordSuccessfulPoll(0);
                return;
            }

            if (detection.Accepted.Count == 0)
            {
                _detector.Acknowledge(
                    detection.Accepted,
                    feed.PayloadSha256);

                _metrics.RecordSuccessfulPoll(0);

                _logger.LogInformation(
                    "Poll tamamlandi: Toplam={Total}, Yeni=0, Eski={Stale}, Sure={Ms}ms",
                    feed.Records.Count,
                    detection.SkippedOutsideRetention,
                    (int)feed.Duration.TotalMilliseconds);

                return;
            }

            // Veriyi once DB yazma kuyruguna kabul ettir.
            // Bu islem verinin DB'ye yazildigi anlamina gelmez.
            await _writeQueue.EnqueueAsync(
                detection.Accepted,
                cancellationToken);

            // Kuyruga kabul edilen konumlari canli duruma uygula.
            _liveStore.Update(detection.Accepted);

            // Canli istemciler icin yayin kuyruguna ekle.
            await _publisher.EnqueueUpdatesAsync(
                detection.Accepted,
                cancellationToken);

            // Tum adimlar basariliysa snapshot'i onayla.
            _detector.Acknowledge(
                detection.Accepted,
                feed.PayloadSha256);

            // Gercek DB yazim sayisini DbWriterWorker takip eder.
            _metrics.RecordSuccessfulPoll(0);

            _logger.LogInformation(
                "Ingestion: Toplam={Total} | Kuyruga Alinan={Queued} | Eski={Stale} | Gecersiz={Invalid} | Sure={Ms}ms",
                feed.Records.Count,
                detection.Accepted.Count,
                detection.SkippedOutsideRetention,
                feed.TotalSkipped,
                (int)feed.Duration.TotalMilliseconds);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _metrics.RecordFailedPoll();

            _logger.LogWarning(
                exception,
                "Ingestion dongusunde hata olustu; sonraki turda tekrar denenecek.");
        }
    }
}
