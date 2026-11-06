
using Google.Protobuf;
using TransitRealtime;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Domain.Entities;
using FeedOccupancyStatus = TransitRealtime.VehiclePosition.Types.OccupancyStatus;
using DomainOccupancyStatus = VehicleTracker.Domain.Enums.OccupancyStatus;

namespace VehicleTracker.Infrastructure.Services;

public class EnturVehicleFeedService : IVehicleFeedService
{
    private readonly HttpClient _httpClient;

    private const string DefaultFeedUrl =
        "https://api.entur.io/realtime/v1/gtfs-rt/vehicle-positions";

    public EnturVehicleFeedService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<VehiclePositionRecord>> FetchLatestPositionsAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            DefaultFeedUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var feedMessage = FeedMessage.Parser.ParseFrom(stream);

        var records = new List<VehiclePositionRecord>(feedMessage.Entity.Count);

        foreach (var entity in feedMessage.Entity)
        {
            if (entity.Vehicle?.Position == null)
            {
                continue;
            }

            var vehicle = entity.Vehicle;
            var position = vehicle.Position;

            var timestamp = vehicle.Timestamp > 0
                ? DateTimeOffset.FromUnixTimeSeconds((long)vehicle.Timestamp).UtcDateTime
                : DateTime.UtcNow;

            records.Add(new VehiclePositionRecord
            {
                VehicleId = vehicle.Vehicle?.Id ?? entity.Id,
                TripId = vehicle.Trip?.TripId,
                RouteId = vehicle.Trip?.RouteId,
                Latitude = position.Latitude,
                Longitude = position.Longitude,
                Bearing = position.HasBearing ? position.Bearing : null,
                Speed = position.HasSpeed ? position.Speed : null,
                OccupancyStatus = vehicle.HasOccupancyStatus
                    ? MapOccupancyStatus(vehicle.OccupancyStatus)
                    : null,
                Timestamp = timestamp
            });
        }

        return records;
    }

    private static DomainOccupancyStatus? MapOccupancyStatus(
        FeedOccupancyStatus occupancyStatus)
    {
        return occupancyStatus switch
        {
            FeedOccupancyStatus.Empty =>
                DomainOccupancyStatus.Empty,

            FeedOccupancyStatus.ManySeatsAvailable =>
                DomainOccupancyStatus.ManySeatsAvailable,

            FeedOccupancyStatus.FewSeatsAvailable =>
                DomainOccupancyStatus.FewSeatsAvailable,

            FeedOccupancyStatus.StandingRoomOnly =>
                DomainOccupancyStatus.StandingRoomOnly,

            FeedOccupancyStatus.CrushedStandingRoomOnly =>
                DomainOccupancyStatus.CrushedStandingRoomOnly,

            FeedOccupancyStatus.Full =>
                DomainOccupancyStatus.Full,

            FeedOccupancyStatus.NotAcceptingPassengers =>
                DomainOccupancyStatus.NotAcceptingPassengers,

            FeedOccupancyStatus.NoDataAvailable =>
                DomainOccupancyStatus.NoDataAvailable,

            FeedOccupancyStatus.NotBoardable =>
                DomainOccupancyStatus.NotBoardable,

            _ => null
        };
    }
}
