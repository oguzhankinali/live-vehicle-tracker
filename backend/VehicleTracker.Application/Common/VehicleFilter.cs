using VehicleTracker.Domain.Entities;

namespace VehicleTracker.Application.Common;

public static class VehicleFilter
{
    public static IEnumerable<VehiclePositionRecord> Apply(
        IEnumerable<VehiclePositionRecord> source,
        string? routeId,
        string? search)
    {
        if (!string.IsNullOrWhiteSpace(routeId))
        {
            source = source.Where(r => string.Equals(r.RouteId, routeId, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            source = source.Where(r =>
                (r.VehicleId != null && r.VehicleId.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                (r.RouteId != null && r.RouteId.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                (r.VehicleLabel != null && r.VehicleLabel.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }

        return source;
    }
}