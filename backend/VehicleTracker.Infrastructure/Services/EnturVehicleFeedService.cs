using Google.Protobuf;
using Microsoft.Extensions.Options;
using TransitRealtime;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Domain.Entities;
using VehicleTracker.Infrastructure.Options;

using FeedOccupancyStatus = TransitRealtime.VehiclePosition.Types.OccupancyStatus;
using FeedVehicleStopStatus = TransitRealtime.VehiclePosition.Types.VehicleStopStatus;
using DomainOccupancyStatus = VehicleTracker.Domain.Enums.OccupancyStatus;
using DomainVehicleStopStatus = VehicleTracker.Domain.Enums.VehicleStopStatus;

namespace VehicleTracker.Infrastructure.Services;

public class EnturVehicleFeedService : IVehicleFeedService
{
    private readonly HttpClient _httpClient;
    private readonly EnturOptions _options;

    public EnturVehicleFeedService(
        HttpClient httpClient,
        IOptions<EnturOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<VehiclePositionRecord>> FetchLatestPositionsAsync(
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _options.FeedUrl);

        request.Headers.TryAddWithoutValidation(
            "ET-Client-Name",
            _options.ClientName);

        using var response = await _httpClient.SendAsync(
            request,
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

            var currentStopSequence = vehicle.HasCurrentStopSequence
                ? vehicle.CurrentStopSequence
                : (uint?)null;

            DomainVehicleStopStatus? currentStatus = currentStopSequence.HasValue
                ? vehicle.HasCurrentStatus
                    ? MapVehicleStopStatus(vehicle.CurrentStatus)
                    : DomainVehicleStopStatus.InTransitTo
                : null;

            var timestamp = vehicle.HasTimestamp
                ? DateTimeOffset.FromUnixTimeSeconds(
                    (long)vehicle.Timestamp).UtcDateTime
                : DateTime.UtcNow;

            records.Add(new VehiclePositionRecord
            {
                VehicleId = vehicle.Vehicle?.Id ?? entity.Id,

                VehicleLabel = vehicle.Vehicle?.HasLabel == true
                    ? vehicle.Vehicle.Label
                    : null,

                TripId = vehicle.Trip?.TripId,
                RouteId = vehicle.Trip?.RouteId,

                DirectionId = vehicle.Trip?.HasDirectionId == true
                    ? vehicle.Trip.DirectionId
                    : null,

                Latitude = position.Latitude,
                Longitude = position.Longitude,

                Bearing = position.HasBearing
                    ? position.Bearing
                    : null,

                Speed = position.HasSpeed
                    ? position.Speed
                    : null,

                StopId = vehicle.HasStopId
                    ? vehicle.StopId
                    : null,

                CurrentStopSequence = currentStopSequence,
                CurrentStatus = currentStatus,

                OccupancyStatus = vehicle.HasOccupancyStatus
                    ? MapOccupancyStatus(vehicle.OccupancyStatus)
                    : null,

                Timestamp = timestamp
            });
        }

        return records;
    }

    private static DomainVehicleStopStatus MapVehicleStopStatus(
        FeedVehicleStopStatus status)
    {
        return status switch
        {
            FeedVehicleStopStatus.IncomingAt =>
                DomainVehicleStopStatus.IncomingAt,

            FeedVehicleStopStatus.StoppedAt =>
                DomainVehicleStopStatus.StoppedAt,

            FeedVehicleStopStatus.InTransitTo =>
                DomainVehicleStopStatus.InTransitTo,

            _ => DomainVehicleStopStatus.InTransitTo
        };
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
