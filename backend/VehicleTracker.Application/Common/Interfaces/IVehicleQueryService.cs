using VehicleTracker.Application.Common.Models;

namespace VehicleTracker.Application.Common.Interfaces;

public interface IVehicleQueryService
{
    IReadOnlyList<VehiclePositionDto> GetLatest(
        string? routeId,
        string? search);
}
