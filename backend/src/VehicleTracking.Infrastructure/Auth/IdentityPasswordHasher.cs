using Microsoft.AspNetCore.Identity;
using VehicleTracking.Application.Abstractions;

namespace VehicleTracking.Infrastructure.Auth;

/// <summary>PBKDF2 through ASP.NET Identity's hasher (only the hasher, not the Identity framework).</summary>
internal sealed class IdentityPasswordHasher : IPasswordHasher
{
    private static readonly object NoUser = new();
    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(NoUser, password);

    public bool Verify(string hash, string password)
    {
        try
        {
            return _hasher.VerifyHashedPassword(NoUser, hash, password) != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
