using Microsoft.AspNetCore.Mvc;
using VehicleTracking.Api.Contracts;
using VehicleTracking.Application.Status;

namespace VehicleTracking.Api.Controllers;

/// <summary>Operational status of the collector.</summary>
[ApiController]
[Route("api/status")]
public sealed class SystemController(SystemStatusService status) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(SystemStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SystemStatusDto>> Get(CancellationToken cancellationToken) =>
        SystemStatusDto.From(await status.GetAsync(cancellationToken));
}
