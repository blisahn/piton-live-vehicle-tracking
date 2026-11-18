namespace VehicleTracking.Application.Auth;

/// <summary>Becomes an HTTP 409.</summary>
public sealed class EmailAlreadyRegisteredException() : Exception("This email address is already registered.");

/// <summary>Wrong email or wrong password; deliberately the same for both. Becomes an HTTP 401.</summary>
public sealed class InvalidCredentialsException() : Exception("Invalid email or password.");

/// <summary>The refresh token is missing, unknown, expired or revoked. Becomes an HTTP 401.</summary>
public sealed class InvalidRefreshTokenException() : Exception("The session is no longer valid. Sign in again.");
