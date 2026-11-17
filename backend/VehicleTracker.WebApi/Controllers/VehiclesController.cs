using Microsoft.AspNetCore.Mvc;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Application.Common.Models;

namespace VehicleTracker.WebApi.Controllers;

[ApiController]
[Route("api/vehicles")]
public sealed class VehiclesController : ControllerBase
{
    private readonly IVehicleQueryService _vehicleQueryService;
    private readonly IVehicleHistoryService _historyService;

    public VehiclesController(
        IVehicleQueryService vehicleQueryService,
        IVehicleHistoryService historyService)
    {
        _vehicleQueryService = vehicleQueryService;
        _historyService = historyService;
    }

    [HttpGet("latest", Name = "GetLatestVehiclePositions")]
    [ProducesResponseType(
        typeof(List<VehiclePositionDto>),
        StatusCodes.Status200OK)]
    public ActionResult<List<VehiclePositionDto>> GetLatest(
        [FromQuery] string? routeId,
        [FromQuery] string? search)
    {
        var result = _vehicleQueryService.GetLatest(routeId, search);

        return Ok(result);
    }

    [HttpGet("{vehicleId}/history", Name = "GetVehicleHistory")]
    [ProducesResponseType(
        typeof(PagedResult<VehiclePositionDto>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PagedResult<VehiclePositionDto>>> GetHistory(
        [FromRoute] string vehicleId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new VehicleHistoryQuery(
            vehicleId,
            from,
            to,
            page,
            pageSize);

        var result = await _historyService.GetHistoryAsync(
            query,
            cancellationToken);

        var response = new PagedResult<VehiclePositionDto>(
            result.Items
                .Select(VehiclePositionDto.From)
                .ToList(),
            result.TotalCount,
            result.Page,
            result.PageSize);

        return Ok(response);
    }
}
