using Microsoft.Extensions.DependencyInjection;
using VehicleTracking.Application.Auth;

namespace VehicleTracking.IntegrationTests;

/// <summary>
/// Refresh-token rotation and reuse detection against the real database. The unit tests cover the
/// rules with a fake store; only a real database shows whether "revoke if still active" is atomic.
/// </summary>
public sealed class RefreshRotationTests(DatabaseFixture db) : DatabaseTest(db)
{
    private async Task<T> WithAuthAsync<T>(Func<AuthService, Task<T>> action)
    {
        // A scope per call, like one HTTP request: its own DbContext and connection.
        using var scope = Db.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AuthService>());
    }

    private Task<AuthResult> RegisterAsync() =>
        WithAuthAsync(auth => auth.RegisterAsync($"user-{Guid.NewGuid():N}@example.com", "integration-pass", default));

    private Task<long> ActiveTokensAsync() =>
        Db.ScalarAsync<long>("select count(*) from refresh_tokens where revoked_at is null");

    [DockerFact]
    public async Task A_refresh_rotates_the_token_and_the_old_one_stops_working()
    {
        var registered = await RegisterAsync();

        var refreshed = await WithAuthAsync(auth => auth.RefreshAsync(registered.RefreshToken, default));

        Assert.NotEqual(registered.RefreshToken, refreshed.RefreshToken);
        Assert.Equal(1L, await ActiveTokensAsync());
    }

    [DockerFact]
    public async Task Reusing_a_rotated_token_ends_the_whole_session()
    {
        var registered = await RegisterAsync();
        var refreshed = await WithAuthAsync(auth => auth.RefreshAsync(registered.RefreshToken, default));

        // Someone replays the old token (it was copied): the new one must die with it.
        await Assert.ThrowsAsync<InvalidRefreshTokenException>(
            () => WithAuthAsync(auth => auth.RefreshAsync(registered.RefreshToken, default)));
        await Assert.ThrowsAsync<InvalidRefreshTokenException>(
            () => WithAuthAsync(auth => auth.RefreshAsync(refreshed.RefreshToken, default)));
        Assert.Equal(0L, await ActiveTokensAsync());
    }

    [DockerFact]
    public async Task Two_concurrent_refreshes_with_one_token_never_both_succeed()
    {
        // Repeated, because a race only shows up some of the time.
        for (var round = 0; round < 20; round++)
        {
            var registered = await RegisterAsync();
            using var start = new SemaphoreSlim(0);

            async Task<AuthResult?> RefreshAsync()
            {
                await start.WaitAsync();
                try
                {
                    return await WithAuthAsync(auth => auth.RefreshAsync(registered.RefreshToken, default));
                }
                catch (InvalidRefreshTokenException)
                {
                    return null;
                }
            }

            var racers = new[] { Task.Run(RefreshAsync), Task.Run(RefreshAsync) };
            start.Release(2);
            var results = await Task.WhenAll(racers);

            // At most one wins; the other looks like a stolen token and revokes the family,
            // so no token of this session is left active.
            Assert.True(results.Count(r => r is not null) <= 1, $"round {round}: both refreshes succeeded");
            Assert.Equal(0L, await Db.ScalarAsync<long>(
                "select count(*) from refresh_tokens t join users u on u.id = t.user_id where u.email = @email and t.revoked_at is null",
                ("email", registered.User.Email)));
        }
    }
}
