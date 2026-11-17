using Microsoft.AspNetCore.Mvc;
using VehicleTracking.Api.Contracts;
using VehicleTracking.Application.History;
using VehicleTracking.Application.Live;

namespace VehicleTracking.Api.Controllers;

/// <summary>Live vehicle positions and the stored history of one vehicle.</summary>
[ApiController]
[Route("api/vehicles")]
[Produces("application/json")]
public sealed class VehiclesController(LiveVehicleService live, VehicleHistoryService history) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(VehicleListDto), StatusCodes.Status200OK)]
    public ActionResult<VehicleListDto> GetLive([FromQuery] string? routeId)
    {
        var snapshot = live.GetCurrent(routeId);
        var vehicles = snapshot.Vehicles.Select(VehicleDto.From).ToList();

        return new VehicleListDto(snapshot.Sequence, vehicles.Count, vehicles);
    }

    // Vehicle keys contain a colon (e.g. RUT:101234); a plain {vehicleKey} route parameter accepts that.
    [HttpGet("{vehicleKey}/history")]
    [ProducesResponseType(typeof(HistoryPageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<HistoryPageDto>> GetHistory(
        string vehicleKey,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] DateTimeOffset? cursor,
        CancellationToken cancellationToken,
        [FromQuery] int limit = VehicleHistoryService.DefaultPageSize)
    {
        var query = new HistoryQuery(vehicleKey, from, to, cursor, limit);
        var page = await history.GetPageAsync(query, cancellationToken);

        return new HistoryPageDto(page.Items.Select(VehicleDto.From).ToList(), page.NextCursor);
    }
}
