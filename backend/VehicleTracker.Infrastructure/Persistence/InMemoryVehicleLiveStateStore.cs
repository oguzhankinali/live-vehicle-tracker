using System.Collections.Concurrent;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Domain.Entities;

namespace VehicleTracker.Infrastructure.Persistence;

public sealed class InMemoryVehicleLiveStateStore : IVehicleLiveStateStore
{
    // Araç ID'sine göre en güncel gözlemi tutan thread-safe sözlük
    private readonly ConcurrentDictionary<string, VehiclePositionRecord> _latestByVehicle = new();

    public IReadOnlyList<VehiclePositionRecord> GetAllLatest()
    {
        return _latestByVehicle.Values.ToList();
    }

    public VehiclePositionRecord? GetLatestByVehicleId(string vehicleId)
    {
        return _latestByVehicle.TryGetValue(vehicleId, out var record) ? record : null;
    }

    public void Update(IReadOnlyList<VehiclePositionRecord> freshRecords)
    {
        foreach (var record in freshRecords)
        {
            _latestByVehicle.AddOrUpdate(
                record.VehicleId,
                record,
                (_, existing) => record.Timestamp >= existing.Timestamp ? record : existing);
        }
    }
}