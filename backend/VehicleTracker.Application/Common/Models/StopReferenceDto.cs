namespace VehicleTracker.Application.Common.Models;

public sealed record StopReferenceDto(
    string Id,
    string Name,
    string? StopPlaceName);