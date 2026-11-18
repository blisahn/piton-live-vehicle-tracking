using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using VehicleTracking.Application.Configuration;

namespace VehicleTracking.Infrastructure.Auth;

/// <summary>The one HS256 key used both to sign and to validate access tokens.</summary>
public sealed class JwtSigningKey
{
    private const int MinKeyBytes = 32;

    public JwtSigningKey(IOptions<AuthOptions> options, ILogger<JwtSigningKey> logger)
    {
        var configured = options.Value.SigningKey;
        var bytes = string.IsNullOrEmpty(configured) ? [] : Encoding.UTF8.GetBytes(configured);
        if (bytes.Length < MinKeyBytes)
        {
            logger.LogWarning(
                "Auth:SigningKey is missing or shorter than {Min} bytes; using a random key. Sessions will end when the process restarts",
                MinKeyBytes);
            bytes = RandomNumberGenerator.GetBytes(64);
        }
        Key = new SymmetricSecurityKey(bytes);
    }

    public SecurityKey Key { get; }
}
