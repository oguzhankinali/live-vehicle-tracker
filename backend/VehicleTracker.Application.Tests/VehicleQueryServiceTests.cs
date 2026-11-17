using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Application.Vehicles;
using VehicleTracker.Domain.Entities;
using Xunit;

namespace VehicleTracker.Application.Tests;

public sealed class VehicleQueryServiceTests
{
    [Fact]
    public void GetLatest_WithoutFilters_ReturnsAllVehicles()
    {
        var service = CreateService();

        var result = service.GetLatest(null, null);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void GetLatest_WithRouteId_FiltersByExactRoute()
    {
        var service = CreateService();

        var result = service.GetLatest("VKT:Line:54", null);

        Assert.Equal(2, result.Count);

        Assert.All(result, vehicle =>
            Assert.Equal("VKT:Line:54", vehicle.RouteId));
    }

    [Fact]
    public void GetLatest_RouteId_IsCaseInsensitive()
    {
        var service = CreateService();

        var result = service.GetLatest("vkt:line:54", null);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void GetLatest_SearchesVehicleIdRouteIdAndLabel()
    {
        var service = CreateService();

        var byVehicleId = service.GetLatest(null, "1001");
        var byRouteId = service.GetLatest(null, "LINE:54");
        var byLabel = service.GetLatest(null, "EXPRESS");

        Assert.Single(byVehicleId);
        Assert.Equal("1001", byVehicleId[0].VehicleId);

        Assert.Equal(2, byRouteId.Count);

        Assert.Single(byLabel);
        Assert.Equal("1003", byLabel[0].VehicleId);
    }

    [Fact]
    public void GetLatest_CombinesRouteAndSearchFilters()
    {
        var service = CreateService();

        var result = service.GetLatest(
            "VKT:Line:54",
            "1002");

        Assert.Single(result);
        Assert.Equal("1002", result[0].VehicleId);
    }

    [Fact]
    public void GetLatest_MapsDomainRecordToDto()
    {
        var service = CreateService();

        var result = service.GetLatest(null, "1001");

        var vehicle = Assert.Single(result);

        Assert.Equal("entity-1", vehicle.FeedEntityId);
        Assert.Equal("1001", vehicle.VehicleId);
        Assert.Equal("VKT:Line:54", vehicle.RouteId);
        Assert.Equal(59.9, vehicle.Latitude);
        Assert.Equal(10.7, vehicle.Longitude);
    }

    private static VehicleQueryService CreateService()
    {
        var records = new List<VehiclePositionRecord>
        {
            new()
            {
                FeedEntityId = "entity-1",
                VehicleId = "1001",
                VehicleLabel = "Local Bus",
                RouteId = "VKT:Line:54",
                Latitude = 59.9,
                Longitude = 10.7
            },
            new()
            {
                FeedEntityId = "entity-2",
                VehicleId = "1002",
                VehicleLabel = "City Bus",
                RouteId = "VKT:Line:54",
                Latitude = 59.8,
                Longitude = 10.6
            },
            new()
            {
                FeedEntityId = "entity-3",
                VehicleId = "1003",
                VehicleLabel = "Airport Express",
                RouteId = "VKT:Line:20",
                Latitude = 59.7,
                Longitude = 10.5
            }
        };

        return new VehicleQueryService(
            new FakeLiveStateStore(records));
    }

    private sealed class FakeLiveStateStore : IVehicleLiveStateStore
    {
        private readonly IReadOnlyList<VehiclePositionRecord> _records;

        public FakeLiveStateStore(
            IReadOnlyList<VehiclePositionRecord> records)
        {
            _records = records;
        }

        public IReadOnlyList<VehiclePositionRecord> GetAllLatest()
        {
            return _records;
        }

        public VehiclePositionRecord? GetLatestByVehicleId(
            string vehicleId)
        {
            return _records.FirstOrDefault(
                record => record.VehicleId == vehicleId);
        }

        public void Update(
            IReadOnlyList<VehiclePositionRecord> freshRecords)
        {
            throw new NotSupportedException();
        }

        public void PruneStaleVehicles(DateTime thresholdUtc)
        {
            throw new NotSupportedException();
        }
    }
}
