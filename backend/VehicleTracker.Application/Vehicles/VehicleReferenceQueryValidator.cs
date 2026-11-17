namespace VehicleTracker.Application.Vehicles;

public sealed class VehicleReferenceQueryValidator
{
    private const int MaxIdentifierLength = 160;

    public void Validate(string? routeId, string? stopId)
    {
        if (string.IsNullOrWhiteSpace(routeId)
            && string.IsNullOrWhiteSpace(stopId))
        {
            throw new ArgumentException(
                "At least one of routeId or stopId is required.");
        }

        if (routeId is { Length: > MaxIdentifierLength }
            || stopId is { Length: > MaxIdentifierLength }
            || (routeId is not null
                && !routeId.Contains(
                    ":Line:",
                    StringComparison.Ordinal))
            || (stopId is not null
                && !stopId.StartsWith(
                    "NSR:Quay:",
                    StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Invalid Entur line or quay identifier.");
        }
    }
}
