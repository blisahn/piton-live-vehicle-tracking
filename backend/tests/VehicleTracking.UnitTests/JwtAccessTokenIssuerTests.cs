using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using VehicleTracking.Application.Auth;
using VehicleTracking.Application.Configuration;
using VehicleTracking.Infrastructure.Auth;

namespace VehicleTracking.UnitTests;

public class JwtAccessTokenIssuerTests
{
    private const string Key = "0123456789abcdef0123456789abcdef-test-key";

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero));
    private readonly AuthOptions _options = new()
    {
        SigningKey = Key,
        Issuer = "test-issuer",
        Audience = "test-audience",
        AccessTokenLifetime = TimeSpan.FromMinutes(15),
    };

    private static readonly User Ada = new()
    {
        Id = Guid.Parse("11111111-2222-3333-4444-555555555555"),
        Email = "ada@example.com",
        PasswordHash = "x",
        Role = Roles.Admin,
        CreatedAt = DateTimeOffset.UnixEpoch,
    };

    private JwtAccessTokenIssuer Issuer(AuthOptions? options = null)
    {
        var wrapped = Options.Create(options ?? _options);
        return new JwtAccessTokenIssuer(new JwtSigningKey(wrapped, NullLogger<JwtSigningKey>.Instance), wrapped, _time);
    }

    private TokenValidationParameters Parameters(string key) => new()
    {
        ValidIssuer = _options.Issuer,
        ValidAudience = _options.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(key)),
        ClockSkew = TimeSpan.Zero,
        RoleClaimType = "role",
        NameClaimType = "email",
        ValidateLifetime = false,
    };

    [Fact]
    public void Token_has_the_expected_claims_and_expiry()
    {
        var token = Issuer().Issue(Ada);

        var jwt = new JsonWebToken(token.Token);
        Assert.Equal(Ada.Id.ToString(), jwt.Subject);
        Assert.Equal("ada@example.com", jwt.GetClaim("email").Value);
        Assert.Equal("admin", jwt.GetClaim("role").Value);
        Assert.False(string.IsNullOrEmpty(jwt.Id));
        Assert.Equal("test-issuer", jwt.Issuer);
        Assert.Contains("test-audience", jwt.Audiences);
        Assert.Equal(_time.GetUtcNow().ToUnixTimeSeconds(), new DateTimeOffset(jwt.IssuedAt).ToUnixTimeSeconds());
        Assert.Equal(_time.GetUtcNow().AddMinutes(15), token.ExpiresAt);
        Assert.Equal(token.ExpiresAt.ToUnixTimeSeconds(), new DateTimeOffset(jwt.ValidTo).ToUnixTimeSeconds());
    }

    [Fact]
    public async Task Token_validates_with_the_same_key_issuer_and_audience()
    {
        var token = Issuer().Issue(Ada);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, Parameters(Key));

        Assert.True(result.IsValid);
        Assert.True(result.ClaimsIdentity.HasClaim("role", "admin"));
    }

    [Fact]
    public async Task Token_does_not_validate_with_another_key()
    {
        var token = Issuer().Issue(Ada);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(
            token.Token, Parameters("another-key-another-key-another-key-0000"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Token_does_not_validate_after_it_expires()
    {
        var token = Issuer().Issue(Ada);
        var parameters = Parameters(Key);
        parameters.ValidateLifetime = true;
        parameters.LifetimeValidator = (_, expires, _, _) => expires > _time.GetUtcNow().UtcDateTime;
        _time.Advance(TimeSpan.FromMinutes(16));

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, parameters);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task A_short_or_missing_signing_key_is_replaced_by_a_random_one()
    {
        var short1 = new AuthOptions { SigningKey = "short" };
        var token = Issuer(short1).Issue(Ada);

        // Not verifiable with the configured (rejected) key.
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, Parameters("short"));
        Assert.False(result.IsValid);
    }
}
