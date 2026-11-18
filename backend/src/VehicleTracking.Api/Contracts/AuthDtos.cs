using VehicleTracking.Application.Auth;

namespace VehicleTracking.Api.Contracts;

/// <summary>Fields are optional here so that missing values become a 400 with a readable detail.</summary>
public sealed record CredentialsRequest(string? Email, string? Password);

public sealed record UserDto(Guid Id, string Email, string Role)
{
    public static UserDto From(UserInfo user) => new(user.Id, user.Email, user.Role);
}

public sealed record AuthResponseDto(string AccessToken, DateTimeOffset ExpiresAt, UserDto User)
{
    public static AuthResponseDto From(AuthResult result) =>
        new(result.AccessToken, result.AccessExpiresAt, UserDto.From(result.User));
}
