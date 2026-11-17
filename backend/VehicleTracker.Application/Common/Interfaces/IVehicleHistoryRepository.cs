using VehicleTracker.Application.Common.Models;
using VehicleTracker.Domain.Entities;

namespace VehicleTracker.Application.Common.Interfaces;

public interface IVehicleHistoryRepository
{
    Task<PagedResult<VehiclePositionRecord>> GetHistoryAsync(
        VehicleHistoryQuery query,
        CancellationToken cancellationToken = default);
}
