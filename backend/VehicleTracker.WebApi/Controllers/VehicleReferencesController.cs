using Microsoft.AspNetCore.Mvc;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Application.Common.Models;
using VehicleTracker.Application.Vehicles;

namespace VehicleTracker.WebApi.Controllers;

[ApiController]
[Route("api/vehicles/reference")]
public sealed class VehicleReferencesController : ControllerBase
{
    private readonly IVehicleReferenceService _referenceService;
    private readonly VehicleReferenceQueryValidator _validator;

    public VehicleReferencesController(
        IVehicleReferenceService referenceService,
        VehicleReferenceQueryValidator validator)
    {
        _referenceService = referenceService;
        _validator = validator;
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
        [FromQuery] string? tripId,
        CancellationToken cancellationToken)
    {
        _validator.Validate(routeId, stopId, tripId);

        var result = await _referenceService.GetAsync(
            routeId,
            stopId,
            tripId,
            cancellationToken);

        return Ok(result);
    }
}
