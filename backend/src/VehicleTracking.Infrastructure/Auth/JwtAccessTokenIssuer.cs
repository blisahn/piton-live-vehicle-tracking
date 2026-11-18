using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.Auth;
using VehicleTracking.Application.Configuration;

namespace VehicleTracking.Infrastructure.Auth;

/// <summary>Issues HS256 JWTs with sub, email, role, jti, iss, aud, iat and exp.</summary>
internal sealed class JwtAccessTokenIssuer(
    JwtSigningKey signingKey,
    IOptions<AuthOptions> options,
    TimeProvider time) : IAccessTokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken Issue(User user)
    {
        var settings = options.Value;
        // exp is whole seconds, so report exactly what is in the token.
        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(time.GetUtcNow().ToUnixTimeSeconds());
        var expiresAt = issuedAt + settings.AccessTokenLifetime;
        expiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresAt.ToUnixTimeSeconds());

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = user.Id.ToString(),
                ["email"] = user.Email,
                ["role"] = user.Role,
                ["jti"] = Guid.NewGuid().ToString(),
            },
            SigningCredentials = new SigningCredentials(signingKey.Key, SecurityAlgorithms.HmacSha256),
        };
        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }
}
