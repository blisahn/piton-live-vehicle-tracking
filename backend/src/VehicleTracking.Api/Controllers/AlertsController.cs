using Microsoft.AspNetCore.Mvc;
using VehicleTracking.Api.Contracts;
using VehicleTracking.Application.Alerts;

namespace VehicleTracking.Api.Controllers;

/// <summary>Service alerts (delays, cancellations, detours) from the operators.</summary>
[ApiController]
[Route("api/alerts")]
public sealed class AlertsController(AlertService alerts, TimeProvider time) : ControllerBase
{
    /// <summary>The current alerts, active ones first.</summary>
    /// <param name="lineId">Only alerts about this line, named directly or through one of their trips.</param>
    /// <param name="stopId">Only alerts about this stop.</param>
    /// <param name="includeInactive">Also return alerts whose active period has not started or has ended.</param>
    [HttpGet]
    [ProducesResponseType(typeof(AlertListDto), StatusCodes.Status200OK)]
    public ActionResult<AlertListDto> Get(string? lineId, string? stopId, bool includeInactive = false)
    {
        // Version first: if the alerts change in between, the client sees an older number and refetches once more.
        var version = alerts.Version;
        var views = alerts.Query(
            string.IsNullOrWhiteSpace(lineId) ? null : lineId,
            string.IsNullOrWhiteSpace(stopId) ? null : stopId,
            includeInactive,
            time.GetUtcNow());
        return new AlertListDto(version, alerts.GeneratedAt, views.Select(AlertDto.From).ToList());
    }
}
