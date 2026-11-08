namespace VehicleTracker.Application.Common.Models;

public sealed record SystemStatusDto(
    DateTime? LastSuccessfulPollUtc,
    long RecordsWrittenLastHour,
    long TotalRecordsWritten,
    int FailedPollAttempts,
    int ConnectedClientCount,
    double UptimeSeconds); 