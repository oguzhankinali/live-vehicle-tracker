using VehicleTracker.Application.Common.Models;

namespace VehicleTracker.Application.Common.Interfaces;

public interface IVehicleReferenceService
{
    Task<VehicleReferenceDto> GetAsync(
        string? routeId,
        string? stopId,
        string? tripId,
        CancellationToken cancellationToken = default);
}
