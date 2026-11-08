using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Domain.Entities;
using VehicleTracker.WebApi.Hubs;

namespace VehicleTracker.WebApi.Services;

public sealed class SignalRVehiclePublisher : BackgroundService, IVehiclePublisher
{
    private readonly IHubContext<VehicleHub> _hubContext;
    private readonly IVehicleLiveStateStore _liveStore;
    private readonly ILogger<SignalRVehiclePublisher> _logger;
    
    // Her bağlantı (ConnectionId) için ayrı bir kuyruk tutuyoruz
    private readonly ConcurrentDictionary<string, Channel<IReadOnlyList<VehiclePositionRecord>>> _clientQueues = new();

    public SignalRVehiclePublisher(
        IHubContext<VehicleHub> hubContext, 
        IVehicleLiveStateStore liveStore,
        ILogger<SignalRVehiclePublisher> logger)
    {
        _hubContext = hubContext;
        _liveStore = liveStore;
        _logger = logger;
    }

    // Yeni istemci bağlandığında onun için bir kuyruk oluştur ve dinlemeye başla
    public void AddClient(string connectionId)
    {
        var channel = Channel.CreateBounded<IReadOnlyList<VehiclePositionRecord>>(new BoundedChannelOptions(50)
        {
            FullMode = BoundedChannelFullMode.DropWrite, // Kuyruk dolarsa yeni yazmayı reddet (Aşağıda handle edeceğiz)
            SingleReader = true,
            SingleWriter = true
        });

        if (_clientQueues.TryAdd(connectionId, channel))
        {
            // Bu istemci için arka planda tüketici (consumer) başlat
            _ = ConsumeClientQueueAsync(connectionId, channel);
        }
    }

    // İstemci koptuğunda kuyruğunu temizle
    public void RemoveClient(string connectionId)
    {
        if (_clientQueues.TryRemove(connectionId, out var channel))
        {
            channel.Writer.TryComplete();
        }
    }

    // ÜRETİCİ: IngestionWorker burayı çağırır. Veriyi tüm istemcilerin kuyruklarına dağıtır.
    public ValueTask EnqueueUpdatesAsync(IReadOnlyList<VehiclePositionRecord> updates, CancellationToken cancellationToken = default)
    {
        if (updates.Count == 0) return ValueTask.CompletedTask;

        foreach (var (connectionId, channel) in _clientQueues)
        {
            if (!channel.Writer.TryWrite(updates))
            {
                // İstemcinin kuyruğu dolmuş (interneti çok yavaş). 
                // Ona eski deltaları atmak yerine, kuyruğunu boşaltıp taze snapshot fırlatacağız.
                _logger.LogWarning("Istemci {ConnectionId} cok yavas! Kuyrugu doldu, Resync (Snapshot) tetikleniyor.", connectionId);
                
                // Kuyruğu boşalt
                while (channel.Reader.TryRead(out _)) { }

                // Taze snapshot'ı kuyruğa at
                var snapshot = _liveStore.GetAllLatest();
                channel.Writer.TryWrite(snapshot);
            }
        }

        return ValueTask.CompletedTask;
    }

    // TÜKETİCİ: Her istemci için ayrı çalışan, kuyruktan alıp SignalR'a basan döngü
    private async Task ConsumeClientQueueAsync(string connectionId, Channel<IReadOnlyList<VehiclePositionRecord>> channel)
    {
        try
        {
            await foreach (var updates in channel.Reader.ReadAllAsync())
            {
                try
                {
                    // Sadece bu spesifik istemciye gönder
                    await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveUpdates", updates);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Istemciye {ConnectionId} veri gonderilemedi: {Message}", connectionId, ex.Message);
                }
            }
        }
        catch (ChannelClosedException)
        {
            // İstemci koptu, döngü biter.
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Artık global bir döngümüz yok, her istemcinin kendi döngüsü var.
        return Task.CompletedTask;
    }
}