using System.Text.Json;
using VehicleTracking.Api.Contracts;

namespace VehicleTracking.Api.Realtime;

// The WebSocket protocol. Every message is one JSON text frame with a "type".
//
// Client to server:
//
//   subscribe  { "type": "subscribe", "bbox": [west, south, east, north] | null,
//                "lines": ["RUT:Line:31", ...], "keys": ["RUT:101234", ...] }
//              Narrows what the client is sent. A vehicle matches when: its key is in `keys`;
//              else, if `lines` is not empty, its routeId is in `lines`; else, if `bbox` is set,
//              its position is inside it (edges inclusive); else (no bbox, no lines) everything.
//              Limits: bbox is 4 finite numbers, lat -90..90, lon -180..180, west < east,
//              south < north; at most 500 lines, 20 keys. An invalid message, or an unknown
//              "type", is ignored (the connection stays). Messages over 64 KB are ignored.
//              At most one subscription is applied per 250 ms; the latest one wins.
//              The initial subscription can also be given on connect:
//              /ws/vehicles?bbox=w,s,e,n&lines=a,b&keys=k1,k2 (comma-separated, URL-decoded,
//              all optional). Each change sends the client a fresh, filtered snapshot.
//              A delta that is empty after filtering is still sent (fromSeq/toSeq, empty lists)
//              so the sequence stays gap-free; a vehicle that leaves the area is sent as removed.
//
// Server to client:
//
//   snapshot   every vehicle as of `seq`. Sent first on every connection, so a reconnecting
//              client is back in sync without knowing what it missed.
//   delta      what changed after the previous message: sequences fromSeq..toSeq merged into one.
//              A client that is at sequence S expects fromSeq == S + 1.
//   heartbeat  sent when nothing else was sent for a while, so a client can tell a quiet
//              connection from a dead one.
//   alerts     the service alerts changed; `version` is the new alerts version. Carries no alerts:
//              the client refetches GET /api/alerts. Sent on connect (current version) and after
//              every change. It never touches vehicle sequence numbers.

internal sealed record SnapshotMessage(long Seq, IReadOnlyList<VehicleDto> Vehicles)
{
    public string Type => "snapshot";
}

internal sealed record DeltaMessage(long FromSeq, long ToSeq, IReadOnlyList<VehicleDto> Upserted, IReadOnlyList<string> Removed)
{
    public string Type => "delta";
}

internal sealed record AlertsMessage(long Version)
{
    public string Type => "alerts";
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
