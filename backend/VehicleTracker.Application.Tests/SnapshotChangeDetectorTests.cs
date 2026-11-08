using VehicleTracker.Application.Common.Models;
using VehicleTracker.Application.Ingestion;
using VehicleTracker.Domain.Entities;
using Xunit;

namespace VehicleTracker.Application.Tests;

public class SnapshotChangeDetectorTests
{
    private static readonly DateTime Now = new(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Retention = TimeSpan.FromHours(3);

    private static VehiclePositionRecord Record(string entityId, DateTime observedAt) => new()
    {
        FeedEntityId = entityId,
        VehicleId = "vehicle-1",
        Timestamp = observedAt,
        ReceivedAtUtc = Now
    };

    private static VehicleFeedResult Feed(
        string sha,
        DateTime? header,
        params VehiclePositionRecord[] records) =>
        new(records, header, Now, 0, sha, TimeSpan.Zero);

    [Fact]
    public void NewEntity_IsAccepted()
    {
        var detector = new SnapshotChangeDetector();

        var result = detector.Detect(
            Feed("SHA_1", Now, Record("e1", Now.AddSeconds(-5))),
            Retention);

        Assert.Equal(SnapshotDisposition.Processed, result.Disposition);
        Assert.Single(result.Accepted);
    }

    [Fact]
    public void UnacknowledgedEntity_IsAcceptedAgainInNextPoll_PreventingDataLoss()
    {
        var detector = new SnapshotChangeDetector();
        var observedAt = Now.AddSeconds(-5);

        // 1. Poll: Kayıt candidate olarak seçildi ama DB yazımı başarısız oldu (Acknowledge çağrılmadı)
        var result1 = detector.Detect(Feed("SHA_1", Now, Record("e1", observedAt)), Retention);
        Assert.Single(result1.Accepted);

        // 2. Poll: Veri kaybı olmaması için aynı SHA tekrar kabul edilmeli!
        var result2 = detector.Detect(Feed("SHA_1", Now, Record("e1", observedAt)), Retention);
        Assert.Single(result2.Accepted);
    }

    [Fact]
    public void AcknowledgedEntity_SameTimestamp_IsSkippedAsNotNewer()
    {
        var detector = new SnapshotChangeDetector();
        var observedAt = Now.AddSeconds(-5);

        var result1 = detector.Detect(Feed("SHA_1", Now, Record("e1", observedAt)), Retention);
        detector.Acknowledge(result1.Accepted, "SHA_1"); // DB'ye başarıyla yazıldı

        // 2. Poll: Artık yazıldığı onaylandı, tekrar yazılmamalı
        var result2 = detector.Detect(Feed("SHA_2", Now.AddSeconds(3), Record("e1", observedAt)), Retention);
        Assert.Empty(result2.Accepted);
        Assert.Equal(1, result2.SkippedNotNewer);
    }

    [Fact]
    public void NewerTimestampForEntity_IsAccepted()
    {
        var detector = new SnapshotChangeDetector();

        var result1 = detector.Detect(Feed("SHA_1", Now, Record("e1", Now.AddSeconds(-20))), Retention);
        detector.Acknowledge(result1.Accepted, "SHA_1");

        var result2 = detector.Detect(Feed("SHA_2", Now.AddSeconds(3), Record("e1", Now.AddSeconds(-10))), Retention);
        Assert.Single(result2.Accepted);
    }

    [Fact]
    public void RecordOlderThanRetention_IsSkipped()
    {
        var detector = new SnapshotChangeDetector();

        var result = detector.Detect(
            Feed("SHA_1", Now, Record("old", Now.AddHours(-4)), Record("fresh", Now.AddMinutes(-1))),
            Retention);

        Assert.Single(result.Accepted);
        Assert.Equal("fresh", result.Accepted[0].FeedEntityId);
        Assert.Equal(1, result.SkippedOutsideRetention);
    }

    [Fact]
    public void IdenticalPayloadSha_AfterAcknowledge_IsSkippedAsDuplicate()
    {
        var detector = new SnapshotChangeDetector();

        var result1 = detector.Detect(Feed("SAME_SHA", Now, Record("e1", Now.AddSeconds(-5))), Retention);
        detector.Acknowledge(result1.Accepted, "SAME_SHA");

        var result2 = detector.Detect(Feed("SAME_SHA", Now, Record("e2", Now.AddSeconds(-1))), Retention);

        Assert.Equal(SnapshotDisposition.DuplicatePayload, result2.Disposition);
        Assert.Empty(result2.Accepted);
    }   
}