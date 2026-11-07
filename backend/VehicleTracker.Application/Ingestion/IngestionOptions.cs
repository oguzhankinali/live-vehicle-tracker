namespace VehicleTracker.Application.Ingestion;

public sealed class IngestionOptions
{
    public const string SectionName = "Ingestion";

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(3);

    // Şartname gereği son 3 saat 
    public TimeSpan Retention { get; set; } = TimeSpan.FromHours(3);

    // Otomatik retention temizleme döngüsü periyodu
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(1);
}