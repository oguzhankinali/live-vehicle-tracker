using VehicleTracker.Domain.Entities;

namespace VehicleTracker.Application.Common.Interfaces;

public interface IVehiclePositionWriter
{
    Task<int> WriteAsync(
        IReadOnlyList<VehiclePositionRecord> records,
        CancellationToken cancellationToken = default);
}