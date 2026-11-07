using VehicleTracker.Domain.Entities;

namespace VehicleTracker.Application.Common.Interfaces;

public interface IVehiclePublisher
{
    // Veriyi kuyruğa bırakır ve anında döner 
    ValueTask EnqueueUpdatesAsync(IReadOnlyList<VehiclePositionRecord> updates, CancellationToken cancellationToken = default);
}