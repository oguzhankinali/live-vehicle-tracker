using Microsoft.Extensions.Logging.Abstractions;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Application.Common.Models;
using VehicleTracker.Domain.Entities;
using VehicleTracker.Infrastructure.Persistence;
using Xunit;

namespace VehicleTracker.Application.Tests;

public sealed class DbWriterWorkerTests
{
    [Fact]
    public async Task GracefulShutdown_WritesActiveAndPendingBatches()
    {
        var queue = new DbWriteQueue(capacity: 2);
        var writer = new ControlledWriter();
        var metrics = new FakeMetrics();

        await queue.EnqueueAsync(CreateBatch("vehicle-1"));
        await queue.EnqueueAsync(CreateBatch("vehicle-2"));

        using var worker = new DbWriterWorker(
            queue,
            writer,
            metrics,
            NullLogger<DbWriterWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);

        // Ilk batch'in DB yazicisina ulastigini bekle.
        await writer.FirstWriteStarted.WaitAsync(
            TimeSpan.FromSeconds(5));

        // Bir batch isleniyor, digeri kuyrukta bekliyor.
        Assert.Equal(1, queue.PendingBatchCount);

        // Ingestion worker kapanirken yaptigi gibi:
        // Artik yeni batch kabul edilmesin.
        queue.Complete();

        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(5));

        // Host kapanis sinyalini gonderiyor.
        var stopTask = worker.StopAsync(timeout.Token);

        // DB yazicisinin devam etmesine izin ver.
        writer.Release();

        await stopTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Kapanis tamamlandiginda iki batch de yazilmis olmali.
        Assert.Equal(2, writer.CompletedWrites);
        Assert.Equal(2, metrics.RecordsWritten);
        Assert.Equal(0, queue.PendingBatchCount);
    }

    [Fact]
    public async Task CompletedQueue_WritesAllBatchesBeforeExiting()
    {
        var queue = new DbWriteQueue(capacity: 3);
        var writer = new ImmediateWriter();
        var metrics = new FakeMetrics();

        await queue.EnqueueAsync(CreateBatch("vehicle-1"));
        await queue.EnqueueAsync(CreateBatch("vehicle-2"));
        await queue.EnqueueAsync(CreateBatch("vehicle-3"));

        // Yeni batch kabul edilmez; mevcut batch'ler okunabilir.
        queue.Complete();

        using var worker = new DbWriterWorker(
            queue,
            writer,
            metrics,
            NullLogger<DbWriterWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);

        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(5));

        // BackgroundService'in asil ExecuteAsync gorevinin
        // gercekten tamamlanmasini bekliyoruz.
        Assert.NotNull(worker.ExecuteTask);

        await worker.ExecuteTask!.WaitAsync(timeout.Token);

        // Islem zaten tamamlandi; ardindan worker'i duzgun kapat.
        await worker.StopAsync(timeout.Token);

        Assert.Equal(3, writer.CompletedWrites);
        Assert.Equal(3, metrics.RecordsWritten);
        Assert.Equal(0, queue.PendingBatchCount);
    }

    private static IReadOnlyList<VehiclePositionRecord> CreateBatch(
        string vehicleId)
    {
        var now = DateTime.UtcNow;

        return new[]
        {
            new VehiclePositionRecord
            {
                FeedEntityId = $"entity-{vehicleId}",
                VehicleId = vehicleId,
                Timestamp = now,
                ReceivedAtUtc = now,
                Latitude = 59.9,
                Longitude = 10.7
            }
        };
    }

    private sealed class ControlledWriter : IVehiclePositionWriter
    {
        private readonly TaskCompletionSource<bool> _started =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool> _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task FirstWriteStarted => _started.Task;

        public int CompletedWrites { get; private set; }

        public void Release()
        {
            _release.TrySetResult(true);
        }

        public async Task<int> WriteAsync(
            IReadOnlyList<VehiclePositionRecord> records,
            CancellationToken cancellationToken = default)
        {
            _started.TrySetResult(true);

            await _release.Task.WaitAsync(cancellationToken);

            CompletedWrites++;

            return records.Count;
        }
    }

    private sealed class ImmediateWriter : IVehiclePositionWriter
    {
        public int CompletedWrites { get; private set; }

        public Task<int> WriteAsync(
            IReadOnlyList<VehiclePositionRecord> records,
            CancellationToken cancellationToken = default)
        {
            CompletedWrites++;

            return Task.FromResult(records.Count);
        }
    }

    private sealed class FakeMetrics : ISystemMetricsService
    {
        public int RecordsWritten { get; private set; }

        public void RecordRecordsWritten(int count)
        {
            RecordsWritten += count;
        }

        public void RecordSuccessfulPoll(int writtenCount)
        {
        }

        public void RecordFailedPoll()
        {
        }

        public void IncrementConnectedClients()
        {
        }

        public void DecrementConnectedClients()
        {
        }

        public SystemStatusDto GetStatus()
        {
            throw new NotSupportedException(
                "System status is not needed in this test.");
        }
    }
}
