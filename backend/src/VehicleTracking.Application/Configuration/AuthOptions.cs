using System.ComponentModel.DataAnnotations;

namespace VehicleTracking.Application.Configuration;

/// <summary>Token signing, lifetimes and the seeded admin account.</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>At least 32 bytes. When empty or shorter, a random key is generated at startup (sessions then end on restart).</summary>
    public string? SigningKey { get; init; }

    [Required]
    public string Issuer { get; init; } = "vehicle-tracking";

    [Required]
    public string Audience { get; init; } = "vehicle-tracking-web";

    [Range(typeof(TimeSpan), "00:00:30", "1.00:00:00")]
    public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromMinutes(15);

    [Range(typeof(TimeSpan), "00:01:00", "365.00:00:00")]
    public TimeSpan RefreshTokenLifetime { get; init; } = TimeSpan.FromDays(7);

    /// <summary>Marks the refresh cookie Secure; leave off for plain http on localhost.</summary>
    public bool CookieSecure { get; init; }

    /// <summary>Failed sign-ins per client address and window; successful ones are never limited.</summary>
    [Range(1, 10_000)]
    public int FailedSignInsPerWindow { get; init; } = 10;

    [Range(typeof(TimeSpan), "00:00:10", "1.00:00:00")]
    public TimeSpan FailedSignInWindow { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Sign-ups per client address and window, against scripted account creation.</summary>
    [Range(1, 10_000)]
    public int RegistrationsPerWindow { get; init; } = 30;

    [Range(typeof(TimeSpan), "00:00:10", "1.00:00:00")]
    public TimeSpan RegistrationWindow { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Refreshes are not password guesses; every page load and tab does one, so they get their own, larger budget.</summary>
    [Range(1, 100_000)]
    public int RefreshesPerMinute { get; init; } = 120;

    [Required]
    public string AdminEmail { get; init; } = string.Empty;

    [Required, MinLength(8)]
    public string AdminPassword { get; init; } = string.Empty;
}
