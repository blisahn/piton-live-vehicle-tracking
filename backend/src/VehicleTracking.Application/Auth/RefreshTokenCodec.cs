using System.Security.Cryptography;
using System.Text;

namespace VehicleTracking.Application.Auth;

/// <summary>Refresh tokens are 256 random bits sent as base64url; the database only ever sees their SHA-256.</summary>
internal static class RefreshTokenCodec
{
    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
