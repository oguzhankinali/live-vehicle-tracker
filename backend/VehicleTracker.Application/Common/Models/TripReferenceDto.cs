namespace VehicleTracker.Application.Common.Models;

public sealed record TripReferenceDto(
    string Id,
    string? PublicCode,
    string? Origin,
    string? Destination);
