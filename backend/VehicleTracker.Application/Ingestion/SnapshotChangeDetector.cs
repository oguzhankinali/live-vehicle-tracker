using VehicleTracker.Application.Common.Models;
using VehicleTracker.Domain.Entities;

namespace VehicleTracker.Application.Ingestion;

public sealed class SnapshotChangeDetector
{
    private static readonly IReadOnlyList<VehiclePositionRecord> NoRecords =
        Array.Empty<VehiclePositionRecord>();

    private readonly Dictionary<string, DateTime> _lastObservedByEntity = new();
    private string? _lastPayloadSha256;

    public DetectionResult Detect(VehicleFeedResult feed, TimeSpan retention)
    {
        if (feed.PayloadSha256 == _lastPayloadSha256)
        {
            return new DetectionResult(
                SnapshotDisposition.DuplicatePayload,
                NoRecords,
                0,
                0);
        }

        var threshold = feed.ReceivedAtUtc - retention;
        var candidates = new List<VehiclePositionRecord>();
        var skippedOutsideRetention = 0;
        var skippedNotNewer = 0;

        foreach (var record in feed.Records)
        {
            if (record.Timestamp < threshold)
            {
                skippedOutsideRetention++;
                continue;
            }

            if (_lastObservedByEntity.TryGetValue(
                    record.FeedEntityId,
                    out var lastObserved)
                && record.Timestamp <= lastObserved)
            {
                skippedNotNewer++;
                continue;
            }

            candidates.Add(record);
        }

        Prune(threshold);

        return new DetectionResult(
            SnapshotDisposition.Processed,
            candidates,
            skippedOutsideRetention,
            skippedNotNewer);
    }

    public void Acknowledge(
        IReadOnlyList<VehiclePositionRecord> processedRecords,
        string payloadSha256)
    {
        foreach (var record in processedRecords)
        {
            if (!_lastObservedByEntity.TryGetValue(
                    record.FeedEntityId,
                    out var current)
                || record.Timestamp > current)
            {
                _lastObservedByEntity[record.FeedEntityId] = record.Timestamp;
            }
        }

        _lastPayloadSha256 = payloadSha256;
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
