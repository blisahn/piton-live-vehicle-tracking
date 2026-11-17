using System.Text.Json;
using VehicleTracking.Api.Contracts;

namespace VehicleTracking.Api.Realtime;

// The WebSocket protocol, server to client. Every message is one JSON text frame with a "type".
//
//   snapshot   every vehicle as of `seq`. Sent first on every connection, so a reconnecting
//              client is back in sync without knowing what it missed.
//   delta      what changed after the previous message: sequences fromSeq..toSeq merged into one.
//              A client that is at sequence S expects fromSeq == S + 1.
//   heartbeat  sent when nothing else was sent for a while, so a client can tell a quiet
//              connection from a dead one.

internal sealed record SnapshotMessage(long Seq, IReadOnlyList<VehicleDto> Vehicles)
{
    public string Type => "snapshot";
}

internal sealed record DeltaMessage(long FromSeq, long ToSeq, IReadOnlyList<VehicleDto> Upserted, IReadOnlyList<string> Removed)
{
    public string Type => "delta";
}

internal sealed record HeartbeatMessage(long Seq, DateTimeOffset ServerTime)
{
    public string Type => "heartbeat";
}

internal static class WireJson
{
    /// <summary>camelCase property names, the same shape the REST endpoints produce.</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
