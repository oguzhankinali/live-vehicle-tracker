using VehicleTracker.Application.Common.Models;

namespace VehicleTracker.Application.Vehicles;

public sealed class VehicleHistoryQueryValidator
{
    private const int MaxPageSize = 500;

    public VehicleHistoryQuery ValidateAndNormalize(
        VehicleHistoryQuery query,
        TimeSpan retention,
        DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(query.VehicleId))
        {
            throw new ArgumentException("Vehicle ID is required.");
        }

        if (retention <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "Retention must be greater than zero.");
        }

        if (query.Page < 1)
        {
            throw new ArgumentException("Page must be greater than zero.");
        }

        if (query.PageSize < 1 || query.PageSize > MaxPageSize)
        {
            throw new ArgumentException(
                $"Page size must be between 1 and {MaxPageSize}.");
        }

        var offset = ((long)query.Page - 1) * query.PageSize;

        if (offset > int.MaxValue)
        {
            throw new ArgumentException(
                "Requested page is too large.");
        }

        var minimumTime = nowUtc - retention;

        var from = query.FromUtc.HasValue
            ? ToUtc(query.FromUtc.Value)
            : minimumTime;

        var to = query.ToUtc.HasValue
            ? ToUtc(query.ToUtc.Value)
            : nowUtc;

        if (from > nowUtc)
        {
            throw new ArgumentException(
                "The start time cannot be in the future.");
        }

        if (from < minimumTime)
        {
            from = minimumTime;
        }

        if (to > nowUtc)
        {
            to = nowUtc;
        }

        if (from > to)
        {
            throw new ArgumentException(
                "The start time cannot be later than the end time.");
        }

        return query with
        {
            FromUtc = from,
            ToUtc = to
        };
    }

    private static DateTime ToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
