using Microsoft.Extensions.Options;
using VehicleTracking.Api.Auth;
using VehicleTracking.Application.Configuration;

namespace VehicleTracking.UnitTests;

public sealed class FailedSignInLimiterTests
{
    private static FailedSignInLimiter Limiter(int failures) => new(Options.Create(new AuthOptions
    {
        AdminEmail = "admin@example.test",
        AdminPassword = "admin12345",
        FailedSignInsPerWindow = failures,
        FailedSignInWindow = TimeSpan.FromMinutes(5),
    }));

    [Fact]
    public void Checking_does_not_use_up_attempts()
    {
        using var limiter = Limiter(failures: 2);

        for (var i = 0; i < 100; i++)
            Assert.False(limiter.IsBlocked("10.0.0.1"));
    }

    [Fact]
    public void An_address_is_blocked_after_its_failures_and_others_are_not()
    {
        using var limiter = Limiter(failures: 2);

        limiter.RecordFailure("10.0.0.1");
        Assert.False(limiter.IsBlocked("10.0.0.1"));
        limiter.RecordFailure("10.0.0.1");

        Assert.True(limiter.IsBlocked("10.0.0.1"));
        Assert.False(limiter.IsBlocked("10.0.0.2"));
    }
}
