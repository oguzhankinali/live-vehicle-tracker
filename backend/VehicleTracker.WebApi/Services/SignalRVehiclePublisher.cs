using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Domain.Entities;
using VehicleTracker.WebApi.Hubs;

namespace VehicleTracker.WebApi.Services;

public sealed class SignalRVehiclePublisher : IVehiclePublisher
{
    private const int QueueCapacity = 50;

    private readonly IHubContext<VehicleHub> _hubContext;
    private readonly IVehicleLiveStateStore _liveStore;
    private readonly ILogger<SignalRVehiclePublisher> _logger;
    private readonly ConcurrentDictionary<string, ClientQueue> _clients = new();

    public SignalRVehiclePublisher(
        IHubContext<VehicleHub> hubContext,
        IVehicleLiveStateStore liveStore,
        ILogger<SignalRVehiclePublisher> logger)
    {
        _hubContext = hubContext;
        _liveStore = liveStore;
        _logger = logger;
    }

    public void AddClient(string connectionId)
    {
        var client = new ClientQueue();
        if (_clients.TryAdd(connectionId, client))
        {
            _ = ConsumeAsync(connectionId, client);
        }
    }

    public void RemoveClient(string connectionId)
    {
        if (_clients.TryRemove(connectionId, out var client))
        {
            client.Queue.Writer.TryComplete();
        }
    }

    public ValueTask EnqueueUpdatesAsync(
        IReadOnlyList<VehiclePositionRecord> updates,
        CancellationToken cancellationToken = default)
    {
        if (updates.Count == 0) return ValueTask.CompletedTask;

        foreach (var (_, client) in _clients)
        {
            // FullMode = Wait olduğu için kuyruk doluysa TryWrite false döner.
            if (!client.Queue.Writer.TryWrite(updates))
            {
                // Üretici sadece bayrağı kaldırır, kuyruğa dokunmaz (SingleReader kuralı korunur)
                Volatile.Write(ref client.NeedsResync, 1);
            }
        }

        return ValueTask.CompletedTask;
    }

    private async Task ConsumeAsync(string connectionId, ClientQueue client)
    {
        try
        {
            await foreach (var updates in client.Queue.Reader.ReadAllAsync())
            {
                if (Interlocked.Exchange(ref client.NeedsResync, 0) == 1)
                {
                    // Tüketici kendi kuyruğunu güvenle boşaltır
                    while (client.Queue.Reader.TryRead(out _)) { }

                    _logger.LogWarning("Istemci {ConnectionId} geride kaldi; taze snapshot (resync) gonderiliyor.", connectionId);

                    await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveSnapshot", _liveStore.GetAllLatest());
                    continue;
                }

                await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveUpdates", updates);
            }
        }
        catch (Exception exception)
        {
            _logger.LogDebug("Istemci {ConnectionId} tuketicisi sonlandi: {Message}", connectionId, exception.Message);
        }
    }

    private sealed class ClientQueue
    {
        public readonly Channel<IReadOnlyList<VehiclePositionRecord>> Queue =
            Channel.CreateBounded<IReadOnlyList<VehiclePositionRecord>>(
                new BoundedChannelOptions(QueueCapacity)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = true,
                    SingleWriter = false
                });

        public int NeedsResync;
    }
}