using System.Threading.Channels;
using VehicleTracker.Domain.Entities;
using VehicleTracker.Infrastructure.Persistence;
using Xunit;

namespace VehicleTracker.Application.Tests;

public sealed class DbWriteQueueTests
{
    [Fact]
    public async Task Enqueue_IncreasesPendingBatchCount()
    {
        var queue = new DbWriteQueue(capacity: 2);

        Assert.Equal(0, queue.PendingBatchCount);

        await queue.EnqueueAsync(CreateBatch("vehicle-1"));

        Assert.Equal(1, queue.PendingBatchCount);

        await queue.EnqueueAsync(CreateBatch("vehicle-2"));

        Assert.Equal(2, queue.PendingBatchCount);
    }

    [Fact]
    public async Task ReadingBatch_DecreasesPendingCount()
    {
        var queue = new DbWriteQueue(capacity: 2);

        await queue.EnqueueAsync(CreateBatch("vehicle-1"));
        await queue.EnqueueAsync(CreateBatch("vehicle-2"));

        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(5));

        await using var reader = queue
            .ReadAllAsync(timeout.Token)
            .GetAsyncEnumerator();

        Assert.True(await reader.MoveNextAsync());

        Assert.Equal(
            "vehicle-1",
            reader.Current[0].VehicleId);

        Assert.Equal(1, queue.PendingBatchCount);
    }

    [Fact]
    public async Task FullQueue_WaitsUntilSpaceIsAvailable()
    {
        var queue = new DbWriteQueue(capacity: 1);

        await queue.EnqueueAsync(CreateBatch("vehicle-1"));

        var secondWrite = queue.EnqueueAsync(
            CreateBatch("vehicle-2")).AsTask();

        // Kapasite dolu; ikinci batch henuz kabul edilmemeli.
        Assert.False(secondWrite.IsCompleted);
        Assert.Equal(1, queue.PendingBatchCount);

        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(5));

        await using var reader = queue
            .ReadAllAsync(timeout.Token)
            .GetAsyncEnumerator();

        Assert.True(await reader.MoveNextAsync());

        // Ilk batch kuyruktan alindi; yer acildi.
        await secondWrite.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, queue.PendingBatchCount);

        Assert.True(await reader.MoveNextAsync());

        Assert.Equal(
            "vehicle-2",
            reader.Current[0].VehicleId);
    }

    [Fact]
    public async Task Complete_DrainsExistingBatchesAndRejectsNewWrites()
    {
        var queue = new DbWriteQueue(capacity: 2);

        await queue.EnqueueAsync(CreateBatch("vehicle-1"));
        await queue.EnqueueAsync(CreateBatch("vehicle-2"));

        queue.Complete();

        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(5));

        var received = new List<string>();

        await foreach (var batch in queue.ReadAllAsync(timeout.Token))
        {
            received.Add(batch[0].VehicleId);
        }

        Assert.Equal(
            new[] { "vehicle-1", "vehicle-2" },
            received);

        Assert.Equal(0, queue.PendingBatchCount);

        await Assert.ThrowsAsync<ChannelClosedException>(
            async () => await queue.EnqueueAsync(
                CreateBatch("vehicle-3")));
    }

    private static IReadOnlyList<VehiclePositionRecord> CreateBatch(
        string vehicleId)
    {
        return new[]
        {
            new VehiclePositionRecord
            {
                FeedEntityId = $"entity-{vehicleId}",
                VehicleId = vehicleId,
                Timestamp = DateTime.UtcNow,
                ReceivedAtUtc = DateTime.UtcNow,
                Latitude = 59.9,
                Longitude = 10.7
            }
        };
    }
}
