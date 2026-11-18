using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using VehicleTracking.Application.Configuration;

namespace VehicleTracking.Api.Auth;

/// <summary>
/// Counts only failed sign-ins per client address. Guessing passwords is what needs slowing down;
/// someone who knows their password is never blocked, however often they sign in.
/// </summary>
public sealed class FailedSignInLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<string> _failures;

    public FailedSignInLimiter(IOptions<AuthOptions> options)
    {
        var auth = options.Value;
        _failures = PartitionedRateLimiter.Create<string, string>(address =>
            RateLimitPartition.GetFixedWindowLimiter(address, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = auth.FailedSignInsPerWindow,
                Window = auth.FailedSignInWindow,
                QueueLimit = 0,
            }));
    }

    /// <summary>True while this address has used up its failed attempts for the current window.</summary>
    public bool IsBlocked(string address)
    {
        // Asking for zero permits only checks whether any are left; it consumes nothing.
        using var lease = _failures.AttemptAcquire(address, 0);
        return !lease.IsAcquired;
    }

    public void RecordFailure(string address)
    {
        using var lease = _failures.AttemptAcquire(address, 1);
    }

    public void Dispose() => _failures.Dispose();
}
