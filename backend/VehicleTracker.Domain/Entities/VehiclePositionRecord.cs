namespace VehicleTracker.Domain.Entities;

public class VehiclePositionRecord
{
    public long Id { get; set; }
    public string VehicleId { get; set; } = string.Empty;
    public string? TripId { get; set; }
    public string? RouteId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public float? Bearing { get; set; }
    public float? Speed { get; set; }
    public string? OccupancyStatus { get; set; }
    public DateTime Timestamp { get; set; }
}