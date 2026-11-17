using VehicleTracker.Domain.Entities;

namespace VehicleTracker.Application.Common.Interfaces;

public interface IVehicleWriteQueue
{
    ValueTask EnqueueAsync(
        IReadOnlyList<VehiclePositionRecord> batch,
        CancellationToken cancellationToken = default);

    void Complete();
}
