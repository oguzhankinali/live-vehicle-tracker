using VehicleTracker.Application.Common.Models;

namespace VehicleTracker.Application.Common.Interfaces;

public interface IVehicleFeedService
{
    Task<VehicleFeedResult> FetchLatestPositionsAsync(
        CancellationToken cancellationToken = default);
}