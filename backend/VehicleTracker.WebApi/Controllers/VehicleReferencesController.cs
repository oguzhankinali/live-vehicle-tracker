using Microsoft.AspNetCore.Mvc;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Application.Common.Models;

namespace VehicleTracker.WebApi.Controllers;

[ApiController]
[Route("api/vehicles/reference")]
public sealed class VehicleReferencesController : ControllerBase
{
    private readonly IVehicleReferenceService _referenceService;

    public VehicleReferencesController(
        IVehicleReferenceService referenceService)
    {
        _referenceService = referenceService;
    }

    [HttpGet]
    [ProducesResponseType(
        typeof(VehicleReferenceDto),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VehicleReferenceDto>> Get(
        [FromQuery] string? routeId,
        [FromQuery] string? stopId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(routeId)
            && string.IsNullOrWhiteSpace(stopId))
        {
            throw new ArgumentException(
                "At least one of routeId or stopId is required.");
        }

        if (routeId is { Length: > 160 }
            || stopId is { Length: > 160 }
            || (routeId is not null
                && !routeId.Contains(":Line:",
                    StringComparison.Ordinal))
            || (stopId is not null
                && !stopId.StartsWith(
                    "NSR:Quay:",
                    StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Invalid Entur line or quay identifier.");
        }

        var result = await _referenceService.GetAsync(
            routeId,
            stopId,
            cancellationToken);

        return Ok(result);
    }
}