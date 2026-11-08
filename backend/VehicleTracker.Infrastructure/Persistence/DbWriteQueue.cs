using System.Threading.Channels;
using VehicleTracker.Domain.Entities;

namespace VehicleTracker.Infrastructure.Persistence;

public sealed class DbWriteQueue
{
    private readonly Channel<IReadOnlyList<VehiclePositionRecord>> _channel;

    public DbWriteQueue(int capacity = 1000)
    {
        _channel = Channel.CreateBounded<IReadOnlyList<VehiclePositionRecord>>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait, // Kuyruk dolarsa Ingestion'ı beklet
                SingleReader = true,
                SingleWriter = false
            });
    }
    public void Complete() => _channel.Writer.TryComplete();

    public ValueTask EnqueueAsync(IReadOnlyList<VehiclePositionRecord> batch, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(batch, ct);

    public IAsyncEnumerable<IReadOnlyList<VehiclePositionRecord>> ReadAllAsync(CancellationToken ct = default) =>
        _channel.Reader.ReadAllAsync(ct);
}