namespace VehicleTracking.Api.Realtime;

internal static class VehicleSocketEndpoint
{
    public const string Path = "/ws/vehicles";

    public static IEndpointRouteBuilder MapVehicleSocket(this IEndpointRouteBuilder endpoints)
    {
        endpoints.Map(Path, async (
            HttpContext context,
            VehicleSocketHub hub,
            TimeProvider time,
            IHostApplicationLifetime lifetime,
            ILogger<VehicleSocketSession> logger) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            using var socket = await context.WebSockets.AcceptWebSocketAsync();

            // Ends the session when the client goes away or the server shuts down.
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(
                context.RequestAborted, lifetime.ApplicationStopping);

            await new VehicleSocketSession(socket, hub, time, logger).RunAsync(stop.Token);
        }).ExcludeFromDescription();

        return endpoints;
    }
}
