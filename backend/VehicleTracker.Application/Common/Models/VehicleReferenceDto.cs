namespace VehicleTracker.Application.Common.Models;

public sealed record VehicleReferenceDto(
    LineReferenceDto? Line,
    StopReferenceDto? Stop);
