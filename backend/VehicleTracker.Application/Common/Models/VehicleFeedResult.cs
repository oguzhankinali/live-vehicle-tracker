using VehicleTracker.Domain.Entities;

namespace VehicleTracker.Application.Common.Models;

public sealed record VehicleFeedResult(
    IReadOnlyList<VehiclePositionRecord> Records,
    DateTime? FeedTimestamp,
    DateTime ReceivedAtUtc,
    long PayloadSizeBytes,
    string PayloadSha256,
    TimeSpan Duration,
    int SkippedWithoutPosition = 0,
    int SkippedWithoutFeedEntityId = 0,
    int SkippedWithoutVehicleId = 0,
    int SkippedWithoutTimestamp = 0,
    int SkippedInvalidCoordinates = 0)
{
    public int TotalSkipped =>
        SkippedWithoutPosition
        + SkippedWithoutFeedEntityId
        + SkippedWithoutVehicleId
        + SkippedWithoutTimestamp
        + SkippedInvalidCoordinates;
}