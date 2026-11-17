using VehicleTracker.Application.Common.Models;
using VehicleTracker.Application.Common.Validation;

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
            throw new VehicleValidationException(
                ValidationErrorCodes.VehicleIdRequired);
        }

        if (retention <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "Retention must be greater than zero.");
        }

        if (query.Page < 1)
        {
            throw new VehicleValidationException(
                ValidationErrorCodes.InvalidPage);
        }

        if (query.PageSize < 1 || query.PageSize > MaxPageSize)
        {
            throw new VehicleValidationException(
                ValidationErrorCodes.InvalidPageSize);
        }

        var offset = ((long)query.Page - 1) * query.PageSize;

        if (offset > int.MaxValue)
        {
            throw new VehicleValidationException(
                ValidationErrorCodes.PageOffsetTooLarge);
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
            throw new VehicleValidationException(
                ValidationErrorCodes.StartTimeInFuture);
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
            throw new VehicleValidationException(
                ValidationErrorCodes.StartTimeAfterEnd);
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
