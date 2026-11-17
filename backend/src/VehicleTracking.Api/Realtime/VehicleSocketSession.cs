using System.Net.WebSockets;
using System.Text.Json;
using VehicleTracking.Api.Contracts;

namespace VehicleTracking.Api.Realtime;

/// <summary>One connected client: sends what its outbox collects until either side ends the connection.</summary>
internal sealed class VehicleSocketSession(
    WebSocket socket,
    VehicleSocketHub hub,
    TimeProvider time,
    ILogger<VehicleSocketSession> logger)
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(10);

    /// <summary>A client that cannot take one message within this time is disconnected; it reconnects and gets a fresh snapshot.</summary>
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(15);

    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(2);

    private long _lastSentSequence;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var outbox = hub.Register();
        using var session = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            var send = SendLoopAsync(outbox, session.Token);
            var receive = ReceiveLoopAsync(session.Token);

            // Whichever ends first (client closed, send failed, server stopping) ends the other.
            await Task.WhenAny(send, receive);
            await session.CancelAsync();
            await Task.WhenAll(send, receive);
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException)
        {
            // The ordinary ways a connection ends.
            logger.LogDebug(ex, "WebSocket session ended");
        }
        finally
        {
            hub.Unregister(outbox);
            await CloseAsync();
        }
    }

    private async Task SendLoopAsync(ClientOutbox outbox, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!await outbox.WaitForWorkAsync(HeartbeatInterval, cancellationToken))
            {
                await SendAsync(new HeartbeatMessage(_lastSentSequence, time.GetUtcNow()), cancellationToken);
                continue;
            }

            // Mapping and serialising happen here, on this client's own loop and outside every lock.
            var (snapshot, delta) = outbox.Take();
            if (snapshot is not null)
            {
                await SendAsync(
                    new SnapshotMessage(snapshot.Sequence, snapshot.Vehicles.Select(VehicleDto.From).ToList()),
                    cancellationToken);
                _lastSentSequence = snapshot.Sequence;
            }
            if (delta is not null)
            {
                await SendAsync(
                    new DeltaMessage(delta.FromSequence, delta.ToSequence, delta.Upserted.Select(VehicleDto.From).ToList(), delta.Removed),
                    cancellationToken);
                _lastSentSequence = delta.ToSequence;
            }
        }
    }

    private async Task SendAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, WireJson.Options);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(SendTimeout);
        await socket.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, timeout.Token);
    }

    /// <summary>The client has nothing to say yet; reading is how we notice that it closed the connection.</summary>
    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];
        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close) return;
        }
    }

    private async Task CloseAsync()
    {
        if (socket.State is not (WebSocketState.Open or WebSocketState.CloseReceived)) return;
        try
        {
            using var timeout = new CancellationTokenSource(CloseTimeout);
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, statusDescription: null, timeout.Token);
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException)
        {
            socket.Abort();
        }
    }
}
