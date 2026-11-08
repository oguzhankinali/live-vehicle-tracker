using VehicleTracker.Domain.Entities;

namespace VehicleTracker.Application.Common.Interfaces;

public interface IVehicleLiveStateStore
{
    IReadOnlyList<VehiclePositionRecord> GetAllLatest();
    VehiclePositionRecord? GetLatestByVehicleId(string vehicleId);
    void Update(IReadOnlyList<VehiclePositionRecord> freshRecords);
    
    // Belirtilen zamandan daha eski olan araçları RAM'den siler
    void PruneStaleVehicles(DateTime thresholdUtc);
}