using System.Security.Claims;

namespace VehicleTracking.Api.Realtime;

internal static class VehicleSocketEndpoint
{
    public const string Path = "/ws/vehicles";

    public static IEndpointRouteBuilder MapVehicleSocket(this IEndpointRouteBuilder endpoints)
    {
        endpoints.Map(Path, async (
            HttpContext context,
            VehicleSocketHub hub,
            WebSocketTraffic traffic,
            TimeProvider time,
            IHostApplicationLifetime lifetime,
            ILogger<VehicleSocketSession> logger) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            // An invalid query string is ignored and the client gets everything, like a client without one.
            if (!VehicleSubscription.TryFromQuery(context.Request.Query, out var initial))
            {
                logger.LogWarning("Ignoring invalid subscription in the WebSocket query string");
                initial = VehicleSubscription.Everything;
            }

            using var socket = await context.WebSockets.AcceptWebSocketAsync();

            // Ends the session when the client goes away or the server shuts down.
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(
                context.RequestAborted, lifetime.ApplicationStopping);

            await new VehicleSocketSession(socket, hub, traffic, time, logger, initial, ExpiryOf(context.User))
                .RunAsync(stop.Token);
        }).RequireAuthorization().ExcludeFromDescription();

        return endpoints;
    }

    /// <summary>The moment the access token the socket was opened with stops being valid.</summary>
    private static DateTimeOffset? ExpiryOf(ClaimsPrincipal user) =>
        long.TryParse(user.FindFirst("exp")?.Value, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;
}
