using VehicleTracker.Domain.Entities;

namespace VehicleTracker.Application.Common.Interfaces;

public interface IVehicleLiveStateStore
{
    // Bellekteki tüm araçların en son bilinen güncel durumlarını döner.
    IReadOnlyList<VehiclePositionRecord> GetAllLatest();

    // Belirli bir aracın en son durumunu döner.
    VehiclePositionRecord? GetLatestByVehicleId(string vehicleId);

    // Yeni gelen gözlemleri belleğe yansıtır (Sadece daha yeni zaman damgası taşıyanları günceller).
    void Update(IReadOnlyList<VehiclePositionRecord> freshRecords);
}