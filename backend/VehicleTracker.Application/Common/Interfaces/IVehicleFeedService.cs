using VehicleTracker.Domain.Entities;

namespace VehicleTracker.Application.Common.Interfaces;

public interface IVehicleFeedService
{
    Task<IReadOnlyList<VehiclePositionRecord>> FetchLatestPositionsAsync(CancellationToken cancellationToken = default);
}