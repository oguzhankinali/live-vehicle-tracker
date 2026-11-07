using VehicleTracker.Application.Common.Models;
using VehicleTracker.Domain.Entities;

namespace VehicleTracker.Application.Ingestion;

public enum SnapshotDisposition
{
    Processed,
    DuplicatePayload
}

public sealed record DetectionResult(
    SnapshotDisposition Disposition,
    IReadOnlyList<VehiclePositionRecord> Accepted,
    int SkippedOutsideRetention,
    int SkippedNotNewer);

public sealed class SnapshotChangeDetector
{
    private static readonly IReadOnlyList<VehiclePositionRecord> NoRecords =
        Array.Empty<VehiclePositionRecord>();

    private readonly Dictionary<string, DateTime> _lastObservedByEntity = new();
    private string? _lastPayloadSha256;

    public DetectionResult Detect(VehicleFeedResult feed, TimeSpan retention)
    {
        // 1. Snapshot düzeyinde tam tekrar kontrolü (SHA256)
        if (feed.PayloadSha256 == _lastPayloadSha256)
        {
            return new DetectionResult(SnapshotDisposition.DuplicatePayload, NoRecords, 0, 0);
        }

        var threshold = feed.ReceivedAtUtc - retention;
        var candidates = new List<VehiclePositionRecord>();
        var skippedOutsideRetention = 0;
        var skippedNotNewer = 0;

        foreach (var record in feed.Records)
        {
            // 2. Saklama penceresi dışındaki (3 saatten eski) kayıtları filtrele
            if (record.Timestamp < threshold)
            {
                skippedOutsideRetention++;
                continue;
            }

            // 3. Entity bazında gözlem zamanı kontrolü
            if (_lastObservedByEntity.TryGetValue(record.FeedEntityId, out var lastObserved)
                && record.Timestamp <= lastObserved)
            {
                skippedNotNewer++;
                continue;
            }

            candidates.Add(record);
        }

        Prune(threshold);

        _lastPayloadSha256 = feed.PayloadSha256;

        return new DetectionResult(
            SnapshotDisposition.Processed,
            candidates,
            skippedOutsideRetention,
            skippedNotNewer);
    }

    /// <summary>
    /// Kritik Güvenlik: Durum sadece veritabanına başarıyla yazılan kayıtlar için güncellenir.
    /// DB yazımı başarısız olursa Acknowledge çağrılmaz ve sonraki poll'da veriler kaybolmaz.
    /// </summary>
    public void Acknowledge(IReadOnlyList<VehiclePositionRecord> processedRecords)
    {
        foreach (var record in processedRecords)
        {
            if (!_lastObservedByEntity.TryGetValue(record.FeedEntityId, out var current)
                || record.Timestamp > current)
            {
                _lastObservedByEntity[record.FeedEntityId] = record.Timestamp;
            }
        }
    }

    private void Prune(DateTime threshold)
    {
        List<string>? expired = null;

        foreach (var (entityId, observedAt) in _lastObservedByEntity)
        {
            if (observedAt < threshold)
            {
                (expired ??= new List<string>()).Add(entityId);
            }
        }

        if (expired is null)
        {
            return;
        }

        foreach (var entityId in expired)
        {
            _lastObservedByEntity.Remove(entityId);
        }
    }
}