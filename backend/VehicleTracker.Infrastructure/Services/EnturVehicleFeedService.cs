using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using TransitRealtime;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Application.Common.Models;
using VehicleTracker.Domain.Entities;
using VehicleTracker.Infrastructure.Options;

using FeedOccupancyStatus = TransitRealtime.VehiclePosition.Types.OccupancyStatus;
using FeedVehicleStopStatus = TransitRealtime.VehiclePosition.Types.VehicleStopStatus;
using DomainOccupancyStatus = VehicleTracker.Domain.Enums.OccupancyStatus;
using DomainVehicleStopStatus = VehicleTracker.Domain.Enums.VehicleStopStatus;

namespace VehicleTracker.Infrastructure.Services;

public class EnturVehicleFeedService : IVehicleFeedService
{
    // Unix epoch üst sınırı (9999-12-31)
    private const ulong MaxUnixSeconds = 253_402_300_799;

    private readonly HttpClient _httpClient;
    private readonly EnturOptions _options;

    public EnturVehicleFeedService(
        HttpClient httpClient,
        IOptions<EnturOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<VehicleFeedResult> FetchLatestPositionsAsync(
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        using var request = new HttpRequestMessage(HttpMethod.Get, _options.FeedUrl);
        request.Headers.TryAddWithoutValidation("ET-Client-Name", _options.ClientName);

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var receivedAtUtc = DateTime.UtcNow;

        var feedMessage = FeedMessage.Parser.ParseFrom(payload);

        DateTime? feedTimestamp = feedMessage.Header is { HasTimestamp: true } header
                                  && header.Timestamp <= MaxUnixSeconds
            ? DateTimeOffset.FromUnixTimeSeconds((long)header.Timestamp).UtcDateTime
            : null;

        var records = new List<VehiclePositionRecord>(feedMessage.Entity.Count);

        var withoutPosition = 0;
        var withoutEntityId = 0;
        var withoutVehicleId = 0;
        var withoutTimestamp = 0;
        var invalidCoordinates = 0;

        foreach (var entity in feedMessage.Entity)
        {
            var record = MapEntity(entity, receivedAtUtc, out var skipReason);

            if (record is not null)
            {
                records.Add(record);
                continue;
            }

            switch (skipReason)
            {
                case SkipReason.NoPosition: withoutPosition++; break;
                case SkipReason.NoEntityId: withoutEntityId++; break;
                case SkipReason.NoVehicleId: withoutVehicleId++; break;
                case SkipReason.NoTimestamp: withoutTimestamp++; break;
                case SkipReason.InvalidCoordinates: invalidCoordinates++; break;
            }
        }

        stopwatch.Stop();

        return new VehicleFeedResult(
            Records: records,
            FeedTimestamp: feedTimestamp,
            ReceivedAtUtc: receivedAtUtc,
            PayloadSizeBytes: payload.LongLength,
            PayloadSha256: Convert.ToHexString(SHA256.HashData(payload)),
            Duration: stopwatch.Elapsed,
            SkippedWithoutPosition: withoutPosition,
            SkippedWithoutFeedEntityId: withoutEntityId,
            SkippedWithoutVehicleId: withoutVehicleId,
            SkippedWithoutTimestamp: withoutTimestamp,
            SkippedInvalidCoordinates: invalidCoordinates);
    }

    private enum SkipReason
    {
        None,
        NoPosition,
        NoEntityId,
        NoVehicleId,
        NoTimestamp,
        InvalidCoordinates
    }

    private static VehiclePositionRecord? MapEntity(
        FeedEntity entity,
        DateTime receivedAtUtc,
        out SkipReason skipReason)
    {
        skipReason = SkipReason.None;

        var vehicle = entity.Vehicle;
        var position = vehicle?.Position;

        if (vehicle is null || position is null)
        {
            skipReason = SkipReason.NoPosition;
            return null;
        }

        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            skipReason = SkipReason.NoEntityId;
            return null;
        }

        var vehicleId = vehicle.Vehicle?.Id;
        if (string.IsNullOrWhiteSpace(vehicleId))
        {
            skipReason = SkipReason.NoVehicleId;
            return null;
        }

        // Zaman damgası olmayan veya 0 olan veriyi sahte zamanla doldurmuyoruz; atlayıp sayıyoruz.
        if (!vehicle.HasTimestamp || vehicle.Timestamp == 0 || vehicle.Timestamp > MaxUnixSeconds)
        {
            skipReason = SkipReason.NoTimestamp;
            return null;
        }

        if (!IsValidCoordinate(position.Latitude, position.Longitude))
        {
            skipReason = SkipReason.InvalidCoordinates;
            return null;
        }

        return new VehiclePositionRecord
        {
            FeedEntityId = entity.Id,
            VehicleId = vehicleId,
            VehicleLabel = NullIfEmpty(vehicle.Vehicle?.Label),

            TripId = NullIfEmpty(vehicle.Trip?.TripId),
            RouteId = NullIfEmpty(vehicle.Trip?.RouteId),

            // uint32 -> long? dönüşümü taşma riskini ortadan kaldırır
            DirectionId = vehicle.Trip is { HasDirectionId: true } trip
                ? (long)trip.DirectionId
                : null,

            Latitude = position.Latitude,
            Longitude = position.Longitude,
            Bearing = position.HasBearing ? position.Bearing : null,
            Speed = position.HasSpeed ? position.Speed : null,

            StopId = NullIfEmpty(vehicle.StopId),
            CurrentStopSequence = vehicle.HasCurrentStopSequence
                ? vehicle.CurrentStopSequence
                : null,

            CurrentStatus = vehicle.HasCurrentStatus
                ? MapVehicleStopStatus(vehicle.CurrentStatus)
                : null,

            OccupancyStatus = vehicle.HasOccupancyStatus
                ? MapOccupancyStatus(vehicle.OccupancyStatus)
                : null,

            Timestamp = DateTimeOffset.FromUnixTimeSeconds((long)vehicle.Timestamp).UtcDateTime,
            ReceivedAtUtc = receivedAtUtc
        };
    }

    private static bool IsValidCoordinate(float latitude, float longitude) =>
        float.IsFinite(latitude)
        && float.IsFinite(longitude)
        && latitude is >= -90f and <= 90f
        && longitude is >= -180f and <= 180f;

    // Protobuf varsayılan "" değerini null'a normalize eder
    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static DomainVehicleStopStatus? MapVehicleStopStatus(FeedVehicleStopStatus status) =>
        status switch
        {
            FeedVehicleStopStatus.IncomingAt => DomainVehicleStopStatus.IncomingAt,
            FeedVehicleStopStatus.StoppedAt => DomainVehicleStopStatus.StoppedAt,
            FeedVehicleStopStatus.InTransitTo => DomainVehicleStopStatus.InTransitTo,
            _ => null
        };

    private static DomainOccupancyStatus? MapOccupancyStatus(FeedOccupancyStatus status) =>
        status switch
        {
            FeedOccupancyStatus.Empty => DomainOccupancyStatus.Empty,
            FeedOccupancyStatus.ManySeatsAvailable => DomainOccupancyStatus.ManySeatsAvailable,
            FeedOccupancyStatus.FewSeatsAvailable => DomainOccupancyStatus.FewSeatsAvailable,
            FeedOccupancyStatus.StandingRoomOnly => DomainOccupancyStatus.StandingRoomOnly,
            FeedOccupancyStatus.CrushedStandingRoomOnly => DomainOccupancyStatus.CrushedStandingRoomOnly,
            FeedOccupancyStatus.Full => DomainOccupancyStatus.Full,
            FeedOccupancyStatus.NotAcceptingPassengers => DomainOccupancyStatus.NotAcceptingPassengers,
            FeedOccupancyStatus.NoDataAvailable => DomainOccupancyStatus.NoDataAvailable,
            FeedOccupancyStatus.NotBoardable => DomainOccupancyStatus.NotBoardable,
            _ => null
        };
}