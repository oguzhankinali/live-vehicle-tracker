using VehicleTracker.Domain.Entities;

namespace VehicleTracker.Application.Common.Interfaces;

public interface IVehicleWriteQueueReader
{
    int PendingBatchCount { get; }

    IAsyncEnumerable<IReadOnlyList<VehiclePositionRecord>> ReadAllAsync(
        CancellationToken cancellationToken = default);
}
