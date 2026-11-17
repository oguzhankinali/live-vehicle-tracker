using VehicleTracker.Application.Common.Validation;

namespace VehicleTracker.Application.Vehicles;

public sealed class VehicleReferenceQueryValidator
{
    private const int MaxIdentifierLength = 160;

    public void Validate(
        string? routeId,
        string? stopId,
        string? tripId = null)
    {
        if (string.IsNullOrWhiteSpace(routeId)
            && string.IsNullOrWhiteSpace(stopId)
            && string.IsNullOrWhiteSpace(tripId))
        {
            throw new VehicleValidationException(
                ValidationErrorCodes.ReferenceIdRequired);
        }

        if (routeId is { Length: > MaxIdentifierLength }
            || stopId is { Length: > MaxIdentifierLength }
            || tripId is { Length: > MaxIdentifierLength }
            || (routeId is not null
                && !routeId.Contains(
                    ":Line:",
                    StringComparison.Ordinal))
            || (stopId is not null
                && !stopId.StartsWith(
                    "NSR:Quay:",
                    StringComparison.Ordinal))
            || (tripId is not null
                && !tripId.Contains(
                    ":ServiceJourney:",
                    StringComparison.Ordinal)))
        {
            throw new VehicleValidationException(
                ValidationErrorCodes.InvalidReferenceId);
        }
    }
}
