namespace VehicleTracker.Infrastructure.Options;

public sealed class EnturOptions
{
    public const string SectionName = "Entur";

    public string FeedUrl { get; set; } =
        "https://api.entur.io/realtime/v1/gtfs-rt/vehicle-positions";

    public string ClientName { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 10;
}