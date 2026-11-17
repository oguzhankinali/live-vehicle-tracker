namespace VehicleTracker.Application.Common.Models;

public sealed record LineReferenceDto(
    string Id,
    string? PublicCode,
    string? Name,
    string? TransportMode,
    string? OperatorName);

public sealed record StopReferenceDto(
    string Id,
    string Name,
    string? StopPlaceName);

public sealed record VehicleReferenceDto(
    LineReferenceDto? Line,
    StopReferenceDto? Stop);
