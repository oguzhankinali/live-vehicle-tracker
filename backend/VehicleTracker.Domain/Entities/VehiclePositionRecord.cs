using VehicleTracker.Domain.Enums;

namespace VehicleTracker.Domain.Entities;

public class VehiclePositionRecord
{
    public string FeedEntityId { get; set; } = string.Empty;

    public string VehicleId { get; set; } = string.Empty;
    public string? VehicleLabel { get; set; }

    public string? TripId { get; set; }
    public string? RouteId { get; set; }

    // bigint / uint32 uyumu için long?
    public long? DirectionId { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public float? Bearing { get; set; }
    public float? Speed { get; set; }

    public string? StopId { get; set; }
    public uint? CurrentStopSequence { get; set; }
    public VehicleStopStatus? CurrentStatus { get; set; }
    public OccupancyStatus? OccupancyStatus { get; set; }

    public DateTime Timestamp { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
}