using Microsoft.AspNetCore.Mvc;
using VehicleTracker.Application.Common;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Application.Common.Models;

namespace VehicleTracker.WebApi.Controllers;

[ApiController]
[Route("api/vehicles")]
public sealed class VehiclesController : ControllerBase
{
    private readonly IVehicleLiveStateStore _liveStore;
    private readonly IVehicleHistoryService _historyService;

    public VehiclesController(
        IVehicleLiveStateStore liveStore,
        IVehicleHistoryService historyService)
    {
        _liveStore = liveStore;
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
        var filtered = VehicleFilter
            .Apply(_liveStore.GetAllLatest(), routeId, search)
            .Select(VehiclePositionDto.From)
            .ToList();

        return Ok(filtered);
    }

    [HttpGet("{vehicleId}/history", Name = "GetVehicleHistory")]
    [ProducesResponseType(
        typeof(PagedResult<VehiclePositionDto>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<VehiclePositionDto>>> GetHistory(
        [FromRoute] string vehicleId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        try
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
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
