namespace VehicleTracker.Application.Ingestion;

public sealed class IngestionOptions
{
    public const string SectionName = "Ingestion";

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(3);

    public TimeSpan Retention { get; set; } = TimeSpan.FromHours(3);

    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(1);

    public TimeSpan ChunkInterval { get; set; } = TimeSpan.FromMinutes(5);
}
