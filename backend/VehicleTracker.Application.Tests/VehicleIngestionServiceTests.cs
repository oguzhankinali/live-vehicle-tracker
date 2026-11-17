using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Application.Common.Models;
using VehicleTracker.Application.Ingestion;
using VehicleTracker.Domain.Entities;
using Xunit;

namespace VehicleTracker.Application.Tests;

public sealed class VehicleIngestionServiceTests
{
    [Fact]
    public async Task SuccessfulPoll_QueuesBeforeUpdatingAndPublishing()
    {
        var setup = new TestSetup();

        await setup.Service.IngestOnceAsync();

        Assert.Equal(
            new[] { "queue", "live", "publish" },
            setup.Events);

        Assert.Equal(1, setup.Queue.AcceptedBatches);
        Assert.Single(setup.LiveStore.GetAllLatest());
        Assert.Equal(1, setup.Publisher.PublishCount);
        Assert.Equal(1, setup.Metrics.SuccessfulPolls);
        Assert.Equal(0, setup.Metrics.FailedPolls);
    }

    [Fact]
    public async Task FailedQueue_DoesNotUpdateLiveStateOrPublish()
    {
        var setup = new TestSetup();
        setup.Queue.FailNext = true;

        await setup.Service.IngestOnceAsync();

        Assert.Empty(setup.Events);
        Assert.Empty(setup.LiveStore.GetAllLatest());
        Assert.Equal(0, setup.Publisher.PublishCount);
        Assert.Equal(0, setup.Metrics.SuccessfulPolls);
        Assert.Equal(1, setup.Metrics.FailedPolls);
    }

    [Fact]
    public async Task FailedQueue_IsRetriedOnNextPoll()
    {
        var setup = new TestSetup();
        setup.Queue.FailNext = true;

        await setup.Service.IngestOnceAsync();

        Assert.Equal(0, setup.Queue.AcceptedBatches);

        await setup.Service.IngestOnceAsync();

        Assert.Equal(2, setup.Feed.FetchCount);
        Assert.Equal(1, setup.Queue.AcceptedBatches);

        Assert.Equal(
            new[] { "queue", "live", "publish" },
            setup.Events);

        Assert.Single(setup.LiveStore.GetAllLatest());
        Assert.Equal(1, setup.Metrics.FailedPolls);
        Assert.Equal(1, setup.Metrics.SuccessfulPolls);
    }

    [Fact]
    public async Task DuplicatePayload_IsNotQueuedTwice()
    {
        var setup = new TestSetup();

        await setup.Service.IngestOnceAsync();
        await setup.Service.IngestOnceAsync();

        Assert.Equal(2, setup.Feed.FetchCount);
        Assert.Equal(1, setup.Queue.AcceptedBatches);
        Assert.Equal(1, setup.Publisher.PublishCount);
        Assert.Equal(2, setup.Metrics.SuccessfulPolls);
    }

    [Fact]
    public async Task FailedPublisher_DoesNotAcknowledgeSnapshot()
    {
        var setup = new TestSetup();
        setup.Publisher.FailNext = true;

        await setup.Service.IngestOnceAsync();

        Assert.Equal(1, setup.Queue.AcceptedBatches);
        Assert.Single(setup.LiveStore.GetAllLatest());
        Assert.Equal(1, setup.Metrics.FailedPolls);

        // Yayın başarısız olduğundan snapshot onaylanmadı.
        // Sonraki polling aynı gözlemi yeniden deneyebilir.
        await setup.Service.IngestOnceAsync();

        Assert.Equal(2, setup.Queue.AcceptedBatches);
        Assert.Equal(1, setup.Publisher.PublishCount);
        Assert.Equal(1, setup.Metrics.SuccessfulPolls);
    }

    private sealed class TestSetup
    {
        public List<string> Events { get; } = new();

        public FakeFeedService Feed { get; }
        public FakeWriteQueue Queue { get; }
        public FakeLiveStore LiveStore { get; }
        public FakePublisher Publisher { get; }
        public FakeMetrics Metrics { get; }

        public VehicleIngestionService Service { get; }

        public TestSetup()
        {
            var now = DateTime.UtcNow;

            var record = new VehiclePositionRecord
            {
                FeedEntityId = "entity-101",
                VehicleId = "vehicle-123",
                Timestamp = now.AddSeconds(-5),
                ReceivedAtUtc = now,
                Latitude = 59.9,
                Longitude = 10.7
            };

            var feedResult = new VehicleFeedResult(
                Records: new[] { record },
                FeedTimestamp: now,
                ReceivedAtUtc: now,
                PayloadSizeBytes: 100,
                PayloadSha256: "test-payload-1",
                Duration: TimeSpan.FromMilliseconds(50));

            Feed = new FakeFeedService(feedResult);
            Queue = new FakeWriteQueue(Events);
            LiveStore = new FakeLiveStore(Events);
            Publisher = new FakePublisher(Events);
            Metrics = new FakeMetrics();

            Service = new VehicleIngestionService(
                Feed,
                Queue,
                LiveStore,
                Metrics,
                Publisher,
                Options.Create(new IngestionOptions()),
                NullLogger<VehicleIngestionService>.Instance);
        }
    }

    private sealed class FakeFeedService : IVehicleFeedService
    {
        private readonly VehicleFeedResult _result;

        public int FetchCount { get; private set; }

        public FakeFeedService(VehicleFeedResult result)
        {
            _result = result;
        }

        public Task<VehicleFeedResult> FetchLatestPositionsAsync(
            CancellationToken cancellationToken = default)
        {
            FetchCount++;
            return Task.FromResult(_result);
        }
    }

    private sealed class FakeWriteQueue : IVehicleWriteQueue
    {
        private readonly List<string> _events;

        public bool FailNext { get; set; }
        public int AcceptedBatches { get; private set; }

        public FakeWriteQueue(List<string> events)
        {
            _events = events;
        }

        public ValueTask EnqueueAsync(
            IReadOnlyList<VehiclePositionRecord> batch,
            CancellationToken cancellationToken = default)
        {
            if (FailNext)
            {
                FailNext = false;
                throw new InvalidOperationException(
                    "Simulated queue failure.");
            }

            AcceptedBatches++;
            _events.Add("queue");

            return ValueTask.CompletedTask;
        }

        public void Complete()
        {
        }
    }

    private sealed class FakeLiveStore : IVehicleLiveStateStore
    {
        private readonly List<string> _events;
        private readonly List<VehiclePositionRecord> _records = new();

        public FakeLiveStore(List<string> events)
        {
            _events = events;
        }

        public IReadOnlyList<VehiclePositionRecord> GetAllLatest()
        {
            return _records;
        }

        public VehiclePositionRecord? GetLatestByVehicleId(
            string vehicleId)
        {
            return _records.LastOrDefault(
                record => record.VehicleId == vehicleId);
        }

        public void Update(
            IReadOnlyList<VehiclePositionRecord> freshRecords)
        {
            _events.Add("live");
            _records.Clear();
            _records.AddRange(freshRecords);
        }

        public void PruneStaleVehicles(DateTime thresholdUtc)
        {
            _records.RemoveAll(
                record => record.Timestamp < thresholdUtc);
        }
    }

    private sealed class FakePublisher : IVehiclePublisher
    {
        private readonly List<string> _events;

        public bool FailNext { get; set; }
        public int PublishCount { get; private set; }

        public FakePublisher(List<string> events)
        {
            _events = events;
        }

        public ValueTask EnqueueUpdatesAsync(
            IReadOnlyList<VehiclePositionRecord> updates,
            CancellationToken cancellationToken = default)
        {
            if (FailNext)
            {
                FailNext = false;
                throw new InvalidOperationException(
                    "Simulated publisher failure.");
            }

            PublishCount++;
            _events.Add("publish");

            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeMetrics : ISystemMetricsService
    {
        public int SuccessfulPolls { get; private set; }
        public int FailedPolls { get; private set; }

        public void RecordSuccessfulPoll(int writtenCount)
        {
            SuccessfulPolls++;
        }

        public void RecordFailedPoll()
        {
            FailedPolls++;
        }

        public void IncrementConnectedClients()
        {
        }

        public void DecrementConnectedClients()
        {
        }

        public void RecordRecordsWritten(int count)
        {
        }

        public SystemStatusDto GetStatus()
        {
            throw new NotSupportedException(
                "System status is not needed in this test.");
        }
    }
}
