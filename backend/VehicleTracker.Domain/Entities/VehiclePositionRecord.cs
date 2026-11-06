using VehicleTracker.Domain.Enums;

namespace VehicleTracker.Domain.Entities;

public class VehiclePositionRecord
{
    public long Id { get; set; }
    
    // Araç Kimlik Bilgileri
    public string VehicleId { get; set; } = string.Empty;
    public string? VehicleLabel { get; set; }

    // Sefer ve Hat Bilgileri
    public string? TripId { get; set; }
    public string? RouteId { get; set; }
    public uint? DirectionId { get; set; }

    // Konum Bilgileri
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public float? Bearing { get; set; }
    public float? Speed { get; set; }

    // Durak ve Durum Bilgileri 
    public string? StopId { get; set; }
    public uint? CurrentStopSequence { get; set; }
    public VehicleStopStatus? CurrentStatus { get; set; }
    public OccupancyStatus? OccupancyStatus { get; set; }

    // Zaman Damgası 
    public DateTime Timestamp { get; set; }
}