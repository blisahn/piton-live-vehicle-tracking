using Microsoft.Extensions.Options;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.Common;
using VehicleTracking.Application.Configuration;

namespace VehicleTracking.Application.Auth;

/// <summary>Registration, sign-in and refresh-token rotation.</summary>
public sealed class AuthService(
    IUserStore users,
    IRefreshTokenStore refreshTokens,
    IAccessTokenIssuer accessTokens,
    IPasswordHasher hasher,
    IOptions<AuthOptions> options,
    TimeProvider time)
{
    internal const int MinPasswordLength = 8;
    internal const int MaxPasswordLength = 128;
    private const int MaxEmailLength = 254;

    // Verified against when the email is unknown, so both failures take about the same time.
    private readonly Lazy<string> _dummyHash = new(() => hasher.Hash("not-a-real-password"));

    public async Task<AuthResult> RegisterAsync(string? email, string? password, CancellationToken cancellationToken)
    {
        var normalized = NormalizeEmail(email);
        if (normalized is null || normalized.Length > MaxEmailLength || !LooksLikeEmail(normalized))
            throw new RequestValidationException("Enter a valid email address.");
        if (password is null || password.Length < MinPasswordLength)
            throw new RequestValidationException($"The password must be at least {MinPasswordLength} characters.");
        if (password.Length > MaxPasswordLength)
            throw new RequestValidationException($"The password must be at most {MaxPasswordLength} characters.");

        if (await users.FindByEmailAsync(normalized, cancellationToken) is not null)
            throw new EmailAlreadyRegisteredException();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalized,
            PasswordHash = hasher.Hash(password),
            Role = Roles.User,
            CreatedAt = time.GetUtcNow(),
        };
        if (!await users.TryAddAsync(user, cancellationToken))
            throw new EmailAlreadyRegisteredException();

        return await StartSessionAsync(user, Guid.NewGuid(), cancellationToken);
    }

    public async Task<AuthResult> LoginAsync(string? email, string? password, CancellationToken cancellationToken)
    {
        var normalized = NormalizeEmail(email);
        var user = normalized is null ? null : await users.FindByEmailAsync(normalized, cancellationToken);
        var passwordOk = hasher.Verify(user?.PasswordHash ?? _dummyHash.Value, password ?? string.Empty);
        if (user is null || !passwordOk) throw new InvalidCredentialsException();

        await refreshTokens.DeleteExpiredAsync(user.Id, time.GetUtcNow(), cancellationToken);
        return await StartSessionAsync(user, Guid.NewGuid(), cancellationToken);
    }

    public async Task<AuthResult> RefreshAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) throw new InvalidRefreshTokenException();

        var now = time.GetUtcNow();
        var current = await refreshTokens.FindByHashAsync(RefreshTokenCodec.Hash(refreshToken), cancellationToken)
            ?? throw new InvalidRefreshTokenException();

        // A used token coming back means it was copied: end the whole session.
        if (current.RevokedAt is not null)
        {
            await refreshTokens.RevokeFamilyAsync(current.FamilyId, now, cancellationToken);
            throw new InvalidRefreshTokenException();
        }
        if (current.ExpiresAt <= now) throw new InvalidRefreshTokenException();

        var user = await users.FindByIdAsync(current.UserId, cancellationToken)
            ?? throw new InvalidRefreshTokenException();

        var (raw, next) = NewRefreshToken(user.Id, current.FamilyId, now);
        await refreshTokens.AddAsync(next, cancellationToken);
        if (!await refreshTokens.TryRevokeAsync(current.Id, next.Id, now, cancellationToken))
        {
            // Someone else rotated the same token a moment ago.
            await refreshTokens.RevokeFamilyAsync(current.FamilyId, now, cancellationToken);
            throw new InvalidRefreshTokenException();
        }
        return Result(user, raw, next);
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;
        var current = await refreshTokens.FindByHashAsync(RefreshTokenCodec.Hash(refreshToken), cancellationToken);
        if (current is not null)
            await refreshTokens.RevokeFamilyAsync(current.FamilyId, time.GetUtcNow(), cancellationToken);
    }

    public async Task<UserInfo?> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId, cancellationToken);
        return user is null ? null : UserInfo.From(user);
    }

    private async Task<AuthResult> StartSessionAsync(User user, Guid familyId, CancellationToken cancellationToken)
    {
        var (raw, token) = NewRefreshToken(user.Id, familyId, time.GetUtcNow());
        await refreshTokens.AddAsync(token, cancellationToken);
        return Result(user, raw, token);
    }

    private (string Raw, RefreshToken Token) NewRefreshToken(Guid userId, Guid familyId, DateTimeOffset now)
    {
        var raw = RefreshTokenCodec.Generate();
        return (raw, new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FamilyId = familyId,
            TokenHash = RefreshTokenCodec.Hash(raw),
            CreatedAt = now,
            ExpiresAt = now + options.Value.RefreshTokenLifetime,
        });
    }

    private AuthResult Result(User user, string rawRefresh, RefreshToken refresh)
    {
        var access = accessTokens.Issue(user);
        return new AuthResult(access.Token, access.ExpiresAt, rawRefresh, refresh.ExpiresAt, UserInfo.From(user));
    }

    internal static string? NormalizeEmail(string? email)
    {
        var trimmed = email?.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static bool LooksLikeEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0 || at != email.LastIndexOf('@') || at == email.Length - 1) return false;
        var domain = email[(at + 1)..];
        return domain.Contains('.') && !domain.StartsWith('.') && !domain.EndsWith('.')
            && !email.Any(char.IsWhiteSpace);
    }
}
