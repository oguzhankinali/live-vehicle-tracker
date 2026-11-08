using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Domain.Entities;

namespace VehicleTracker.Infrastructure.Persistence;

public sealed class DbWriterWorker : BackgroundService
{
    private readonly DbWriteQueue _queue;
    private readonly IVehiclePositionWriter _writer;
    private readonly ILogger<DbWriterWorker> _logger;

    public DbWriterWorker(DbWriteQueue queue, IVehiclePositionWriter writer, ILogger<DbWriterWorker> logger)
    {
        _queue = queue;
        _writer = writer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DbWriterWorker baslatildi. DB yazimlari arka planda islenecek.");

        try
        {
            await foreach (var batch in _queue.ReadAllAsync(stoppingToken))
            {
                await WriteWithRetryAsync(batch, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("DbWriterWorker durduruluyor.");
        }
    }

    private async Task WriteWithRetryAsync(IReadOnlyList<VehiclePositionRecord> batch, CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds(1);
        
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var inserted = await _writer.WriteAsync(batch, ct);
                return; // Başarılı, döngüden çık ve sıradaki batch'i al
            }
            catch (PostgresException ex) when (ex.SqlState != null && (ex.SqlState.StartsWith("22") || ex.SqlState.StartsWith("23")))
            {
                _logger.LogError(ex, "Kalici DB hatasi, {Count} kayit atildi (VERI KAYBI).", batch.Count);
                return;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "DB yazimi basarisiz. {Delay}sn sonra tekrar denenecek. Bekleyen kayit: {Count}. Hata: {Message}", 
                    delay.TotalSeconds, batch.Count, ex.Message);
                
                await Task.Delay(delay, ct);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 30)); // Exponential backoff (max 30sn bekle)
            }
        }
    }
}