using Microsoft.AspNetCore.Mvc;
using VehicleTracking.Api.Contracts;
using VehicleTracking.Application.Common;
using VehicleTracking.Application.Transit;
using VehicleTracking.Application.TripUpdates;

namespace VehicleTracking.Api.Controllers;

/// <summary>Line and stop names and planned trip routes.</summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public sealed class TransitController(ReferenceDataService referenceData, TripRouteService tripRoutes, TripProgressService tripProgress, TimeProvider time) : ControllerBase
{
    /// <summary>Keeps one request, and the catalog request behind it, small.</summary>
    private const int MaxStopIds = 200;

    /// <summary>Every line seen in the feed so far.</summary>
    [HttpGet("lines")]
    [ProducesResponseType(typeof(LineListDto), StatusCodes.Status200OK)]
    public ActionResult<LineListDto> GetLines() =>
        new LineListDto(referenceData.Lines.Select(LineDto.From).ToList());

    [HttpGet("stops")]
    [ProducesResponseType(typeof(StopListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<StopListDto>> GetStops([FromQuery] string[]? ids, CancellationToken cancellationToken)
    {
        ids ??= [];
        if (ids.Length > MaxStopIds)
            throw new RequestValidationException($"At most {MaxStopIds} stop ids can be requested at once.");

        var stops = await referenceData.GetStopsAsync(ids, cancellationToken);
        return new StopListDto(stops.Select(StopDto.From).ToList());
    }

    // Trip ids contain colons (e.g. ATB:ServiceJourney:1); a plain {tripId} route parameter accepts that.
    [HttpGet("trips/{tripId}/route")]
    [ProducesResponseType(typeof(TripRouteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<TripRouteDto>> GetTripRoute(string tripId, CancellationToken cancellationToken)
    {
        var route = await tripRoutes.GetAsync(tripId, cancellationToken);
        if (route is null)
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Trip not found", detail: $"No planned route is known for trip '{tripId}'.");

        return TripRouteDto.From(route);
    }

    /// <summary>Delays and expected times of a running trip, from the GTFS-Realtime Trip Updates feed.</summary>
    /// <remarks>Stop names and times are filled in when the planned route can be looked up; the delays are returned regardless.</remarks>
    [HttpGet("trips/{tripId}/progress")]
    [ProducesResponseType(typeof(TripProgressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TripProgressDto>> GetTripProgress(string tripId, CancellationToken cancellationToken)
    {
        var progress = await tripProgress.GetAsync(tripId, time.GetUtcNow(), cancellationToken);
        if (progress is null)
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Trip not found", detail: $"Trip '{tripId}' is not in the trip updates feed.");

        return TripProgressDto.From(progress);
    }
}
