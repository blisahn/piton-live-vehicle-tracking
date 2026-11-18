namespace VehicleTracking.Application.Auth;

/// <summary>An account that may sign in. The email is stored lower-cased.</summary>
public sealed class User
{
    public required Guid Id { get; init; }

    public required string Email { get; init; }

    public required string PasswordHash { get; init; }

    public required string Role { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}

public static class Roles
{
    public const string User = "user";
    public const string Admin = "admin";
}

/// <summary>What clients may know about a user.</summary>
public sealed record UserInfo(Guid Id, string Email, string Role)
{
    public static UserInfo From(User user) => new(user.Id, user.Email, user.Role);
}

/// <summary>One refresh token as stored: only its hash, never the token itself.</summary>
public sealed class RefreshToken
{
    public required Guid Id { get; init; }

    public required Guid UserId { get; init; }

    /// <summary>All tokens handed out by rotating one login share this id; revoking it ends the session.</summary>
    public required Guid FamilyId { get; init; }

    public required string TokenHash { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset? RevokedAt { get; set; }

    public Guid? ReplacedById { get; set; }
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

public sealed record AuthResult(
    string AccessToken,
    DateTimeOffset AccessExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshExpiresAt,
    UserInfo User);
