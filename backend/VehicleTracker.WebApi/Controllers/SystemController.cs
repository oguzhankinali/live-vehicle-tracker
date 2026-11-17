using Microsoft.AspNetCore.Mvc;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Application.Common.Models;

namespace VehicleTracker.WebApi.Controllers;

[ApiController]
[Route("api/system")]
public sealed class SystemController : ControllerBase
{
    private readonly ISystemMetricsService _metrics;

    public SystemController(ISystemMetricsService metrics)
    {
        _metrics = metrics;
    }

    [HttpGet("status", Name = "GetSystemStatus")]
    [ProducesResponseType(
        typeof(SystemStatusDto),
        StatusCodes.Status200OK)]
    public ActionResult<SystemStatusDto> GetStatus()
    {
        return Ok(_metrics.GetStatus());
    }
}
