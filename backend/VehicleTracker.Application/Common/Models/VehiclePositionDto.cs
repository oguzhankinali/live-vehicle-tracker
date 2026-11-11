using VehicleTracker.Domain.Entities;
using VehicleTracker.Domain.Enums;

namespace VehicleTracker.Application.Common.Models;

public sealed record VehiclePositionDto(
    string FeedEntityId,
    string VehicleId,
    string? VehicleLabel,
    string? TripId,
    string? RouteId,
    long? DirectionId,
    double Latitude,
    double Longitude,
    float? Bearing,
    float? Speed,
    string? StopId,
    uint? CurrentStopSequence,
    VehicleStopStatus? CurrentStatus,
    OccupancyStatus? OccupancyStatus,
    DateTime Timestamp,
    DateTime ReceivedAtUtc)
{
    public static VehiclePositionDto From(VehiclePositionRecord r) => new(
        r.FeedEntityId,
        r.VehicleId,
        r.VehicleLabel,
        r.TripId,
        r.RouteId,
        r.DirectionId,
        r.Latitude,
        r.Longitude,
        r.Bearing,
        r.Speed,
        r.StopId,
        r.CurrentStopSequence,
        r.CurrentStatus,
        r.OccupancyStatus,
        r.Timestamp,
        r.ReceivedAtUtc);
}