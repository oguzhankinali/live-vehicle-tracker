namespace VehicleTracker.Application.Common.Models;

public sealed record LineReferenceDto(
    string Id,
    string? PublicCode,
    string? Name,
    string? TransportMode,
    string? OperatorName);