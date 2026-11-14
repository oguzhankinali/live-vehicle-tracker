
using System.Reflection;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Domain.Entities;
using VehicleTracker.WebApi.Hubs;
using VehicleTracker.WebApi.Services;

// In-process test of the real publisher, without a WebSocket transport.
var blockedSend = new TaskCompletionSource(
    TaskCreationOptions.RunContinuationsAsynchronously);

var slowSendEntered = new TaskCompletionSource(
    TaskCreationOptions.RunContinuationsAsynchronously);

int fastUpdates = 0;
int slowSnapshots = 0;

var slowProxy = Fake<ISingleClientProxy>((method, args) =>
{
    if (method.Name != "SendCoreAsync")
        throw new NotSupportedException(method.Name);

    string message = (string)args![0]!;

    if (message == "ReceiveUpdates")
    {
        slowSendEntered.TrySetResult();

        // Keep first slow send in-flight.
        return blockedSend.Task;
    }

    if (message == "ReceiveSnapshot")
    {
        Interlocked.Increment(ref slowSnapshots);
        return Task.CompletedTask;
    }

    throw new InvalidOperationException(
        $"Unexpected method: {message}");
});

var fastProxy = Fake<ISingleClientProxy>((method, args) =>
{
    if (method.Name != "SendCoreAsync")
        throw new NotSupportedException(method.Name);

    if ((string)args![0]! == "ReceiveUpdates")
        Interlocked.Increment(ref fastUpdates);

    return Task.CompletedTask;
});

var clients = Fake<IHubClients>((method, args) =>
{
    if (method.Name == "Client")
    {
        return (string)args![0]! == "slow"
            ? slowProxy
            : fastProxy;
    }

    throw new NotSupportedException(method.Name);
});

var hub = Fake<IHubContext<VehicleHub>>((method, args) =>
{
    if (method.Name == "get_Clients")
        return clients;

    throw new NotSupportedException(method.Name);
});

var liveStore = new SampleLiveStateStore();

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Warning);
});

var publisher = new SignalRVehiclePublisher(
    hub,
    liveStore,
    loggerFactory.CreateLogger<SignalRVehiclePublisher>());

publisher.AddClient("slow");
publisher.AddClient("fast");

var update = new[]
{
    new VehiclePositionRecord
    {
        FeedEntityId = "test-1",
        VehicleId = "BUS-42",
        Timestamp = DateTime.UtcNow
    }
};

Console.WriteLine(
    "[TEST] Enqueue first update, wait until slow SendCoreAsync is blocked...");

await publisher.EnqueueUpdatesAsync(update);

if (!await WaitUntil(
    () => slowSendEntered.Task.IsCompleted, 5000))
{
    throw new Exception(
        "Slow client send was not reached (check client proxy setup)");
}

Console.WriteLine(
    "[TEST] Send 80 further batches while slow client's send remains blocked...");

for (int i = 0; i < 80; i++)
{
    await publisher.EnqueueUpdatesAsync(update);

    int expected = i + 2;

    if (!await WaitUntil(
        () => Volatile.Read(ref fastUpdates) >= expected,
        5000))
    {
        throw new Exception(
            $"Fast client did not receive batch {expected}. " +
            $"Received: {Volatile.Read(ref fastUpdates)}");
    }
}

if (!await WaitUntil(
    () => Volatile.Read(ref fastUpdates) == 81, 5000))
{
    throw new Exception(
        $"Fast client blocked or lost batches: {Volatile.Read(ref fastUpdates)}/81");
}

Console.WriteLine(
    "[TEST] Unblock slow client and wait for fresh snapshot...");

blockedSend.TrySetResult();

if (!await WaitUntil(
    () => Volatile.Read(ref slowSnapshots) >= 1, 5000))
{
    throw new Exception(
        "Slow client was not resynchronized with a snapshot");
}

Console.WriteLine(
    $"PASS: fast client received {fastUpdates}/81 batches while slow client was blocked.");

Console.WriteLine(
    $"PASS: slow client received {slowSnapshots} recovery snapshot(s) after blockage.");

Console.WriteLine(
    "LIMIT: in-process publisher behavior; not an end-to-end WebSocket stress test.");

publisher.RemoveClient("slow");
publisher.RemoveClient("fast");

static async Task<bool> WaitUntil(
    Func<bool> predicate,
    int timeoutMs)
{
    var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

    while (DateTime.UtcNow < deadline)
    {
        if (predicate())
            return true;

        await Task.Delay(20);
    }

    return predicate();
}

static T Fake<T>(
    Func<MethodInfo, object?[]?, object?> dispatch)
    where T : class
{
    var proxy = DispatchProxy.Create<T, TestProxy>();

    ((TestProxy)(object)proxy).Handler = dispatch;

    return proxy;
}

public class TestProxy : DispatchProxy
{
    public Func<MethodInfo, object?[]?, object?>? Handler
    {
        get;
        set;
    }

    protected override object? Invoke(
        MethodInfo? targetMethod,
        object?[]? args)
    {
        if (targetMethod is null || Handler is null)
            throw new InvalidOperationException();

        return Handler(targetMethod, args);
    }
}

public sealed class SampleLiveStateStore : IVehicleLiveStateStore
{
    private readonly VehiclePositionRecord[] _items =
    [
        new()
        {
            FeedEntityId = "test-1",
            VehicleId = "BUS-42",
            Timestamp = DateTime.UtcNow
        }
    ];

    public IReadOnlyList<VehiclePositionRecord> GetAllLatest()
        => _items;

    public VehiclePositionRecord? GetLatestByVehicleId(
        string vehicleId)
        => _items.FirstOrDefault(
            r => r.VehicleId == vehicleId);

    public void Update(
        IReadOnlyList<VehiclePositionRecord> freshRecords)
    {
    }

    public void PruneStaleVehicles(
        DateTime thresholdUtc)
    {
    }
}
