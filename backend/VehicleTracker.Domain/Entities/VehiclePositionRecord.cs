
using VehicleTracker.Domain.Enums;

namespace VehicleTracker.Domain.Entities;

public class VehiclePositionRecord
{
    // GTFS-Realtime feed icindeki entity kimligi.
    public string FeedEntityId { get; set; } = string.Empty;

    // Arac kimligi. Harita ve arac gecmisi bu alan uzerinden sorgulanir.
    public string VehicleId { get; set; } = string.Empty;
    public string? VehicleLabel { get; set; }

    // Sefer ve hat bilgileri.
    public string? TripId { get; set; }
    public string? RouteId { get; set; }
    public uint? DirectionId { get; set; }

    // Konum bilgileri.
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public float? Bearing { get; set; }
    public float? Speed { get; set; }

    // Durak ve durum bilgileri.
    public string? StopId { get; set; }
    public uint? CurrentStopSequence { get; set; }
    public VehicleStopStatus? CurrentStatus { get; set; }
    public OccupancyStatus? OccupancyStatus { get; set; }

    // Entur tarafindan bildirilen gozlem zamani (UTC).
    public DateTime Timestamp { get; set; }

    // Kaydin uygulamamiz tarafindan alindigi zaman (UTC).
    public DateTime ReceivedAtUtc { get; set; }
}
