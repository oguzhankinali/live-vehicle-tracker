using VehicleTracker.Application.Common;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Application.Common.Models;

namespace VehicleTracker.Application.Vehicles;

public sealed class VehicleQueryService : IVehicleQueryService
{
    private readonly IVehicleLiveStateStore _liveStore;

    public VehicleQueryService(IVehicleLiveStateStore liveStore)
    {
        _liveStore = liveStore;
    }

    public IReadOnlyList<VehiclePositionDto> GetLatest(
        string? routeId,
        string? search)
    {
        return VehicleFilter
            .Apply(_liveStore.GetAllLatest(), routeId, search)
            .Select(VehiclePositionDto.From)
            .ToList();
    }
}
