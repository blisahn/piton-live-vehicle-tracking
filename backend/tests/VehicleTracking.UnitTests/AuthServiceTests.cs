using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.Auth;
using VehicleTracking.Application.Common;
using VehicleTracking.Application.Configuration;

namespace VehicleTracking.UnitTests;

public class AuthServiceTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeUserStore _users = new();
    private readonly FakeRefreshTokenStore _tokens = new();
    private readonly AuthService _auth;

    public AuthServiceTests()
    {
        _auth = new AuthService(
            _users, _tokens, new FakeIssuer(_time), new FakeHasher(),
            Options.Create(new AuthOptions { RefreshTokenLifetime = TimeSpan.FromDays(7) }), _time);
    }

    [Fact]
    public async Task Register_creates_a_user_with_a_lower_cased_email_and_the_user_role()
    {
        var result = await _auth.RegisterAsync("  Ada@Example.COM ", "password123", default);

        Assert.Equal("ada@example.com", result.User.Email);
        Assert.Equal(Roles.User, result.User.Role);
        Assert.False(string.IsNullOrEmpty(result.RefreshToken));
        Assert.Equal(_time.GetUtcNow().AddDays(7), result.RefreshExpiresAt);
        Assert.Single(_users.All);
        Assert.NotEqual("password123", _users.All[0].PasswordHash);
    }

    [Fact]
    public async Task Register_rejects_a_duplicate_email_regardless_of_case()
    {
        await _auth.RegisterAsync("ada@example.com", "password123", default);

        await Assert.ThrowsAsync<EmailAlreadyRegisteredException>(
            () => _auth.RegisterAsync("ADA@example.com", "password123", default));
    }

    [Fact]
    public async Task Register_rejects_a_short_password()
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => _auth.RegisterAsync("ada@example.com", "short", default));
        Assert.Empty(_users.All);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-at-sign")]
    [InlineData("a@b")]
    [InlineData("a b@example.com")]
    [InlineData("a@@example.com")]
    public async Task Register_rejects_an_invalid_email(string? email)
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => _auth.RegisterAsync(email, "password123", default));
    }

    [Fact]
    public async Task Login_fails_the_same_way_for_a_wrong_password_and_an_unknown_email()
    {
        await _auth.RegisterAsync("ada@example.com", "password123", default);

        var wrongPassword = await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => _auth.LoginAsync("ada@example.com", "wrong-password", default));
        var unknownEmail = await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => _auth.LoginAsync("nobody@example.com", "password123", default));

        Assert.Equal(wrongPassword.Message, unknownEmail.Message);
    }

    [Fact]
    public async Task Login_succeeds_with_the_right_credentials_and_starts_a_new_family()
    {
        var registered = await _auth.RegisterAsync("ada@example.com", "password123", default);

        var login = await _auth.LoginAsync("ADA@example.com", "password123", default);

        Assert.Equal(registered.User.Id, login.User.Id);
        Assert.Equal(2, _tokens.All.Select(t => t.FamilyId).Distinct().Count());
    }

    [Fact]
    public async Task Login_deletes_the_expired_refresh_tokens_of_that_user()
    {
        await _auth.RegisterAsync("ada@example.com", "password123", default);
        _time.Advance(TimeSpan.FromDays(8));

        await _auth.LoginAsync("ada@example.com", "password123", default);

        Assert.Single(_tokens.All);
    }

    [Fact]
    public async Task Refresh_rotates_the_token_inside_the_same_family()
    {
        var first = await _auth.RegisterAsync("ada@example.com", "password123", default);

        var second = await _auth.RefreshAsync(first.RefreshToken, default);

        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.Equal(2, _tokens.All.Count);
        var old = _tokens.All[0];
        var rotated = _tokens.All[1];
        Assert.NotNull(old.RevokedAt);
        Assert.Equal(rotated.Id, old.ReplacedById);
        Assert.Equal(old.FamilyId, rotated.FamilyId);
        Assert.Null(rotated.RevokedAt);
    }

    [Fact]
    public async Task Refresh_stores_only_a_hash_of_the_token()
    {
        var result = await _auth.RegisterAsync("ada@example.com", "password123", default);

        Assert.DoesNotContain(_tokens.All, t => t.TokenHash == result.RefreshToken);
    }

    [Fact]
    public async Task Reusing_a_revoked_refresh_token_revokes_the_whole_family()
    {
        var first = await _auth.RegisterAsync("ada@example.com", "password123", default);
        var second = await _auth.RefreshAsync(first.RefreshToken, default);

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _auth.RefreshAsync(first.RefreshToken, default));

        Assert.All(_tokens.All, t => Assert.NotNull(t.RevokedAt));
        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _auth.RefreshAsync(second.RefreshToken, default));
    }

    [Fact]
    public async Task Reuse_does_not_touch_the_sessions_of_other_logins()
    {
        var first = await _auth.RegisterAsync("ada@example.com", "password123", default);
        var other = await _auth.LoginAsync("ada@example.com", "password123", default);
        await _auth.RefreshAsync(first.RefreshToken, default);

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _auth.RefreshAsync(first.RefreshToken, default));

        await _auth.RefreshAsync(other.RefreshToken, default);
    }

    [Fact]
    public async Task Refresh_rejects_an_expired_token()
    {
        var result = await _auth.RegisterAsync("ada@example.com", "password123", default);
        _time.Advance(TimeSpan.FromDays(7));

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _auth.RefreshAsync(result.RefreshToken, default));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown-token")]
    public async Task Refresh_rejects_a_missing_or_unknown_token(string? token)
    {
        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _auth.RefreshAsync(token, default));
    }

    [Fact]
    public async Task Logout_revokes_the_family()
    {
        var first = await _auth.RegisterAsync("ada@example.com", "password123", default);
        var second = await _auth.RefreshAsync(first.RefreshToken, default);

        await _auth.LogoutAsync(second.RefreshToken, default);

        Assert.All(_tokens.All, t => Assert.NotNull(t.RevokedAt));
        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _auth.RefreshAsync(second.RefreshToken, default));
    }

    [Fact]
    public async Task Logout_without_a_token_does_nothing()
    {
        await _auth.LogoutAsync(null, default);
        await _auth.LogoutAsync("unknown", default);
    }

    [Fact]
    public async Task GetUser_returns_the_user_or_null()
    {
        var result = await _auth.RegisterAsync("ada@example.com", "password123", default);

        Assert.Equal("ada@example.com", (await _auth.GetUserAsync(result.User.Id, default))?.Email);
        Assert.Null(await _auth.GetUserAsync(Guid.NewGuid(), default));
    }

    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(string password) => "hash:" + password;

        public bool Verify(string hash, string password) => hash == "hash:" + password;
    }

    private sealed class FakeIssuer(TimeProvider time) : IAccessTokenIssuer
    {
        public AccessToken Issue(User user) =>
            new("access-" + user.Id, time.GetUtcNow().AddMinutes(15));
    }

    private sealed class FakeUserStore : IUserStore
    {
        public List<User> All { get; } = [];

        public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult(All.FirstOrDefault(u => u.Email == email));

        public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(All.FirstOrDefault(u => u.Id == id));

        public Task<bool> TryAddAsync(User user, CancellationToken cancellationToken)
        {
            if (All.Any(u => u.Email == user.Email)) return Task.FromResult(false);
            All.Add(user);
            return Task.FromResult(true);
        }
    }

    private sealed class FakeRefreshTokenStore : IRefreshTokenStore
    {
        public List<RefreshToken> All { get; } = [];

        public Task AddAsync(RefreshToken token, CancellationToken cancellationToken)
        {
            All.Add(token);
            return Task.CompletedTask;
        }

        public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
            Task.FromResult(All.FirstOrDefault(t => t.TokenHash == tokenHash));

        public Task<bool> TryRevokeAsync(Guid id, Guid? replacedById, DateTimeOffset at, CancellationToken cancellationToken)
        {
            var token = All.FirstOrDefault(t => t.Id == id && t.RevokedAt is null);
            if (token is null) return Task.FromResult(false);
            token.RevokedAt = at;
            token.ReplacedById = replacedById;
            return Task.FromResult(true);
        }

        public Task RevokeFamilyAsync(Guid familyId, DateTimeOffset at, CancellationToken cancellationToken)
        {
            foreach (var token in All.Where(t => t.FamilyId == familyId && t.RevokedAt is null))
                token.RevokedAt = at;
            return Task.CompletedTask;
        }

        public Task DeleteExpiredAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
        {
            All.RemoveAll(t => t.UserId == userId && t.ExpiresAt < now);
            return Task.CompletedTask;
        }
    }
}
