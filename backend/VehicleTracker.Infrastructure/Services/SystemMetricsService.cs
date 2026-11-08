using System.Collections.Concurrent;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Application.Common.Models;

namespace VehicleTracker.Infrastructure.Services;

public sealed class SystemMetricsService : ISystemMetricsService
{
    private readonly DateTime _startTimeUtc = DateTime.UtcNow;
    private DateTime? _lastSuccessfulPollUtc;
    private int _failedPollAttempts;
    private int _connectedClientCount;
    private long _totalRecordsWritten;

    // Son 1 saati dakikalık dilimler (bucket) halinde tutan thread-safe sayaç
    private readonly ConcurrentDictionary<long, long> _hourlyMinuteBuckets = new();

    public void RecordSuccessfulPoll(int _)
    {
        _lastSuccessfulPollUtc = DateTime.UtcNow;
    }

    public void RecordRecordsWritten(int count)
    {
        if (count <= 0) return;

        Interlocked.Add(ref _totalRecordsWritten, count);

        var currentMinute = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
        _hourlyMinuteBuckets.AddOrUpdate(currentMinute, count, (_, existing) => existing + count);

        PruneOldBuckets(currentMinute);
    }

    public void RecordFailedPoll()
    {
        Interlocked.Increment(ref _failedPollAttempts);
    }

    public void IncrementConnectedClients()
    {
        Interlocked.Increment(ref _connectedClientCount);
    }

    public void DecrementConnectedClients()
    {
        Interlocked.Decrement(ref _connectedClientCount);
    }

    public SystemStatusDto GetStatus()
    {
        var currentMinute = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
        PruneOldBuckets(currentMinute);

        var writtenLastHour = _hourlyMinuteBuckets.Values.Sum();

        return new SystemStatusDto(
            LastSuccessfulPollUtc: _lastSuccessfulPollUtc,
            RecordsWrittenLastHour: writtenLastHour,
            TotalRecordsWritten: Interlocked.Read(ref _totalRecordsWritten),
            FailedPollAttempts: _failedPollAttempts,
            ConnectedClientCount: Math.Max(0, _connectedClientCount),
            UptimeSeconds: (DateTime.UtcNow - _startTimeUtc).TotalSeconds); // TotalSeconds olarak dönüyoruz
    }

    private void PruneOldBuckets(long currentMinute)
    {
        var thresholdMinute = currentMinute - 60;
        foreach (var key in _hourlyMinuteBuckets.Keys)
        {
            if (key < thresholdMinute)
            {
                _hourlyMinuteBuckets.TryRemove(key, out _);
            }
        }
    }
}