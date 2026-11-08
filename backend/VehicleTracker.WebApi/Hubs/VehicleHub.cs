using Microsoft.AspNetCore.SignalR;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.WebApi.Services;

namespace VehicleTracker.WebApi.Hubs;

public sealed class VehicleHub : Hub
{
    private readonly IVehicleLiveStateStore _liveStore;
    private readonly ISystemMetricsService _metrics;
    private readonly SignalRVehiclePublisher _publisher;

    public VehicleHub(
        IVehicleLiveStateStore liveStore, 
        ISystemMetricsService metrics,
        SignalRVehiclePublisher publisher)
    {
        _liveStore = liveStore;
        _metrics = metrics;
        _publisher = publisher;
    }

    public override async Task OnConnectedAsync()
    {
        _metrics.IncrementConnectedClients();
        
        // İstemci için özel kuyruk oluştur
        _publisher.AddClient(Context.ConnectionId);

        // İlk Snapshot'ı gönder
        var snapshot = _liveStore.GetAllLatest();
        await Clients.Caller.SendAsync("ReceiveSnapshot", snapshot);

        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _metrics.DecrementConnectedClients();
        
        // İstemci koptuğunda kuyruğunu temizle
        _publisher.RemoveClient(Context.ConnectionId);
        
        return base.OnDisconnectedAsync(exception);
    }
}