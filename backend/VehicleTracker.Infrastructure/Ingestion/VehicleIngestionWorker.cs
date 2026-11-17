using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Application.Ingestion;

namespace VehicleTracker.Infrastructure.Ingestion;

public sealed class VehicleIngestionWorker : BackgroundService
{
    private readonly VehicleIngestionService _ingestionService;
    private readonly IVehicleWriteQueue _writeQueue;
    private readonly IngestionOptions _options;
    private readonly ILogger<VehicleIngestionWorker> _logger;

    public VehicleIngestionWorker(
        VehicleIngestionService ingestionService,
        IVehicleWriteQueue writeQueue,
        IOptions<IngestionOptions> options,
        ILogger<VehicleIngestionWorker> logger)
    {
        _ingestionService = ingestionService;
        _writeQueue = writeQueue;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "VehicleIngestionWorker baslatildi. Polling: {Interval}sn, Saklama: {Retention}saat",
            _options.PollInterval.TotalSeconds,
            _options.Retention.TotalHours);

        using var timer = new PeriodicTimer(_options.PollInterval);

        try
        {
            while (!stoppingToken.IsCancellationRequested &&
                   await timer.WaitForNextTickAsync(stoppingToken))
            {
                await _ingestionService.IngestOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "VehicleIngestionWorker durduruluyor.");
        }
        finally
        {
            // Artik kuyruğa yeni batch eklenmeyecek.
            // DbWriterWorker kalan batch'leri islemeye devam edebilir.
            _writeQueue.Complete();
        }
    }
}
