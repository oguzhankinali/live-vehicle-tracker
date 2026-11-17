using Microsoft.AspNetCore.SignalR;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.WebApi.Services;

namespace VehicleTracker.WebApi.Hubs;

public sealed class VehicleHub : Hub
{
    private readonly IVehicleLiveStateStore _liveStore;
    private readonly ISystemMetricsService _metrics;
    private readonly IVehicleClientRegistry _clientRegistry;

    public VehicleHub(
        IVehicleLiveStateStore liveStore,
        ISystemMetricsService metrics,
        IVehicleClientRegistry clientRegistry)
    {
        _liveStore = liveStore;
        _metrics = metrics;
        _clientRegistry = clientRegistry;
    }

    public override async Task OnConnectedAsync()
    {
        _metrics.IncrementConnectedClients();

        _clientRegistry.AddClient(Context.ConnectionId);

        var snapshot = _liveStore.GetAllLatest();

        await Clients.Caller.SendAsync(
            VehicleHubMethods.ReceiveSnapshot,
            snapshot);

        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _metrics.DecrementConnectedClients();

        _clientRegistry.RemoveClient(Context.ConnectionId);

        return base.OnDisconnectedAsync(exception);
    }
}
