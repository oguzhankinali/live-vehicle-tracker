using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Domain.Entities;
using VehicleTracker.WebApi.Hubs;

namespace VehicleTracker.WebApi.Services;

// Hem IVehiclePublisher arayüzünü uygular (Üretici), hem de BackgroundService'dir (Tüketici)
public sealed class SignalRVehiclePublisher : BackgroundService, IVehiclePublisher
{
    private readonly IHubContext<VehicleHub> _hubContext;
    private readonly ILogger<SignalRVehiclePublisher> _logger;
    
    // 100 kapasiteli, eski veriyi atan (DropOldest) bir kuyruk
    private readonly Channel<IReadOnlyList<VehiclePositionRecord>> _channel;

    public SignalRVehiclePublisher(IHubContext<VehicleHub> hubContext, ILogger<SignalRVehiclePublisher> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
        
        _channel = Channel.CreateBounded<IReadOnlyList<VehiclePositionRecord>>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest, // Kuyruk dolarsa en eski güncellemeyi at (Canlı yayında eski veri önemsizdir)
            SingleReader = true,
            SingleWriter = false
        });
    }

    // ÜRETİCİ (IngestionWorker burayı çağırır, anında döner)
    public async ValueTask EnqueueUpdatesAsync(IReadOnlyList<VehiclePositionRecord> updates, CancellationToken cancellationToken = default)
    {
        if (updates.Count == 0) return;

        await _channel.Writer.WriteAsync(updates, cancellationToken);
    }

    // TÜKETİCİ (Arka planda sürekli çalışır, kuyruktan alır ve SignalR'a basar)
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SignalRVehiclePublisher (Tuketici) baslatildi.");

        try
        {
            await foreach (var updates in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    // Yavaş istemci burada bekletse bile, IngestionWorker etkilenmez
                    await _hubContext.Clients.All.SendAsync("ReceiveUpdates", updates, stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning("SignalR yayini sirasinda hata: {Message}", ex.Message);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("SignalRVehiclePublisher durduruluyor.");
        }
    }
}