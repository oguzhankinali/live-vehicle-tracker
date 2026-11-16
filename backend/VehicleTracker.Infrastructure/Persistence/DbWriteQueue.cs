using System.Threading.Channels;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Domain.Entities;

namespace VehicleTracker.Infrastructure.Persistence;

public sealed class DbWriteQueue : IVehicleWriteQueue
{
    private const int DefaultCapacity = 1000;

    private readonly Channel<IReadOnlyList<VehiclePositionRecord>> _channel;

    public DbWriteQueue(int capacity = DefaultCapacity)
    {
        _channel = Channel.CreateBounded<IReadOnlyList<VehiclePositionRecord>>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
    }

    public int PendingBatchCount => _channel.Reader.Count;

    public void Complete()
    {
        _channel.Writer.TryComplete();
    }

    public ValueTask EnqueueAsync(
        IReadOnlyList<VehiclePositionRecord> batch,
        CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(batch, cancellationToken);
    }

    public IAsyncEnumerable<IReadOnlyList<VehiclePositionRecord>> ReadAllAsync(
        CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
