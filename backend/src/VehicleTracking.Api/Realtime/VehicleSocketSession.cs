using System.Net.WebSockets;
using System.Text.Json;
using VehicleTracking.Api.Contracts;

namespace VehicleTracking.Api.Realtime;

/// <summary>One connected client: sends what its outbox collects until either side ends the connection.</summary>
internal sealed class VehicleSocketSession(
    WebSocket socket,
    VehicleSocketHub hub,
    WebSocketTraffic traffic,
    TimeProvider time,
    ILogger<VehicleSocketSession> logger,
    VehicleSubscription? initialSubscription = null,
    DateTimeOffset? tokenExpiresAt = null)
{
    /// <summary>Client messages above this size are ignored (the rest of the message is discarded, the connection stays).</summary>
    internal const int MaxClientMessageBytes = 64 * 1024;

    internal static readonly TimeSpan MinSubscriptionInterval = TimeSpan.FromMilliseconds(250);

    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(10);

    /// <summary>A client that cannot take one message within this time is disconnected; it reconnects and gets a fresh snapshot.</summary>
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(15);

    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Custom close code: the access token the socket was opened with has expired.</summary>
    internal const int TokenExpiredCloseStatus = 4401;

    private bool _tokenExpired;
    private long _lastSentSequence;
    private long _bytesSent;
    private VehicleSubscription _subscription = initialSubscription ?? VehicleSubscription.Everything;
    private readonly SubscriptionFilter _filter = new();

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var outbox = hub.Register();
        using var throttle = new SubscriptionThrottle(time, MinSubscriptionInterval, subscription =>
        {
            // Store first, then ask for the snapshot: the send loop reads the subscription after it
            // takes the snapshot, so it can never filter the new snapshot with the old subscription.
            Volatile.Write(ref _subscription, subscription);
            hub.Resnapshot(outbox);
        });
        using var session = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            var send = SendLoopAsync(outbox, session.Token);
            var receive = ReceiveLoopAsync(throttle, session.Token);
            var expiry = ExpiryLoopAsync(session.Token);

            // Whichever ends first (client closed, send failed, token expired, server stopping) ends the others.
            await Task.WhenAny(send, receive, expiry);
            await session.CancelAsync();
            await Task.WhenAll(send, receive, expiry);
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException)
        {
            // The ordinary ways a connection ends.
            logger.LogDebug(ex, "WebSocket session ended");
        }
        finally
        {
            hub.Unregister(outbox);
            logger.LogDebug("WebSocket session ended after sending {Bytes} bytes", Interlocked.Read(ref _bytesSent));
            await CloseAsync();
        }
    }

    /// <summary>Ends the session when the token expires; the client refreshes it and reconnects.</summary>
    private async Task ExpiryLoopAsync(CancellationToken cancellationToken)
    {
        if (tokenExpiresAt is not { } expiresAt)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return;
        }

        var remaining = expiresAt - time.GetUtcNow();
        if (remaining > TimeSpan.Zero) await Task.Delay(remaining, time, cancellationToken);
        _tokenExpired = true;
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
            var (snapshot, delta, alertsVersion) = outbox.Take();

            // Read after Take (see the comment where the subscription is stored).
            var subscription = Volatile.Read(ref _subscription);
            if (snapshot is not null)
            {
                var vehicles = _filter.FilterSnapshot(snapshot.Vehicles, subscription);
                await SendAsync(
                    new SnapshotMessage(snapshot.Sequence, vehicles.Select(VehicleDto.From).ToList()),
                    cancellationToken);
                _lastSentSequence = snapshot.Sequence;
            }
            if (delta is not null)
            {
                // Sent even when the filter leaves it empty: the client reconnects on a sequence gap.
                var (upserted, removed) = _filter.FilterDelta(delta.Upserted, delta.Removed, subscription);
                await SendAsync(
                    new DeltaMessage(delta.FromSequence, delta.ToSequence, upserted.Select(VehicleDto.From).ToList(), removed),
                    cancellationToken);
                _lastSentSequence = delta.ToSequence;
            }
            if (alertsVersion is { } version)
                await SendAsync(new AlertsMessage(version), cancellationToken);
        }
    }

    private async Task SendAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, WireJson.Options);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(SendTimeout);
        await socket.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, timeout.Token);
        Interlocked.Add(ref _bytesSent, payload.Length);
        traffic.Add(payload.Length);
    }

    /// <summary>Reads client messages (the only one is "subscribe"); it is also how we notice that the client closed.</summary>
    private async Task ReceiveLoopAsync(SubscriptionThrottle throttle, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        using var message = new MemoryStream();
        var tooBig = false;
        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close) return;

            // Multi-frame messages are assembled; one over the cap is dropped as a whole.
            if (!tooBig)
            {
                if (message.Length + result.Count > MaxClientMessageBytes) { tooBig = true; message.SetLength(0); }
                else message.Write(buffer, 0, result.Count);
            }
            if (!result.EndOfMessage) continue;

            if (tooBig) logger.LogWarning("Ignoring a WebSocket message over {Limit} bytes", MaxClientMessageBytes);
            else if (result.MessageType == WebSocketMessageType.Text) HandleMessage(message.ToArray(), throttle);
            message.SetLength(0);
            tooBig = false;
        }
    }

    private void HandleMessage(byte[] bytes, SubscriptionThrottle throttle)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("type", out var type)
                || type.ValueKind != JsonValueKind.String
                || type.GetString() != "subscribe")
            {
                logger.LogDebug("Ignoring an unknown WebSocket message");
                return;
            }
            if (!VehicleSubscription.TryParse(root, out var subscription))
            {
                logger.LogDebug("Ignoring an invalid subscribe message");
                return;
            }
            throttle.Submit(subscription);
        }
        catch (JsonException ex)
        {
            logger.LogDebug(ex, "Ignoring a WebSocket message that is not valid JSON");
        }
    }

    private async Task CloseAsync()
    {
        if (socket.State is not (WebSocketState.Open or WebSocketState.CloseReceived)) return;
        try
        {
            using var timeout = new CancellationTokenSource(CloseTimeout);
            if (_tokenExpired)
                await socket.CloseAsync((WebSocketCloseStatus)TokenExpiredCloseStatus, "token expired", timeout.Token);
            else
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, statusDescription: null, timeout.Token);
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException)
        {
            socket.Abort();
        }
    }
}
