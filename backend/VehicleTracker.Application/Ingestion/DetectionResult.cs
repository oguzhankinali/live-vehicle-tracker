using VehicleTracker.Domain.Entities;

namespace VehicleTracker.Application.Ingestion;

public sealed record DetectionResult(
    SnapshotDisposition Disposition,
    IReadOnlyList<VehiclePositionRecord> Accepted,
    int SkippedOutsideRetention,
    int SkippedNotNewer);
