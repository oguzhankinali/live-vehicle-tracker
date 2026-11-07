using System.Collections.Concurrent;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Domain.Entities;

namespace VehicleTracker.Infrastructure.Persistence;

public sealed class InMemoryVehicleLiveStateStore : IVehicleLiveStateStore
{
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

    public void PruneStaleVehicles(DateTime thresholdUtc)
    {
        var collection = (ICollection<KeyValuePair<string, VehiclePositionRecord>>)_latestByVehicle;
        
        foreach (var kvp in _latestByVehicle)
        {
            if (kvp.Value.Timestamp < thresholdUtc)
            {
                collection.Remove(kvp);
            }
        }
    }
}