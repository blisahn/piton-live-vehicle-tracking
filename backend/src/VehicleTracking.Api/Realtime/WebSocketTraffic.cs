using VehicleTracking.Application.Abstractions;

namespace VehicleTracking.Api.Realtime;

/// <summary>Counts bytes sent over all sockets in one-second buckets and sums the last 60 of them.</summary>
internal sealed class WebSocketTraffic(TimeProvider time) : IWebSocketTraffic
{
    private const int Buckets = 60;
    private readonly object _sync = new();
    private readonly long[] _bytes = new long[Buckets];
    private readonly long[] _second = new long[Buckets];

    public void Add(long bytes)
    {
        var now = time.GetUtcNow().ToUnixTimeSeconds();
        var index = (int)(now % Buckets);
        lock (_sync)
        {
            if (_second[index] != now)
            {
                _second[index] = now;
                _bytes[index] = 0;
            }
            _bytes[index] += bytes;
        }
    }

    public long BytesSentLastMinute
    {
        get
        {
            var now = time.GetUtcNow().ToUnixTimeSeconds();
            long total = 0;
            lock (_sync)
            {
                for (var i = 0; i < Buckets; i++)
                    if (_second[i] > now - Buckets && _second[i] <= now) total += _bytes[i];
            }
            return total;
        }
    }
}
