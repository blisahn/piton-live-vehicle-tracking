using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.Auth;
using VehicleTracking.Application.Configuration;
using VehicleTracking.Infrastructure.Persistence;

namespace VehicleTracking.Infrastructure.Auth;

/// <summary>Creates the configured admin account once the database is migrated, if it does not exist yet.</summary>
internal sealed class AdminSeeder(
    IServiceScopeFactory scopes,
    DatabaseReadiness readiness,
    IOptions<AuthOptions> options,
    TimeProvider time,
    ILogger<AdminSeeder> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await readiness.WaitAsync(stoppingToken);
            while (true)
            {
                try
                {
                    await SeedAsync(stoppingToken);
                    return;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Seeding the admin account failed, retrying in {Delay}", RetryDelay);
                }
                await Task.Delay(RetryDelay, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    internal async Task SeedAsync(CancellationToken ct)
    {
        var settings = options.Value;
        var email = AdminEmailOf(settings);
        using var scope = scopes.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserStore>();
        if (await users.FindByEmailAsync(email, ct) is not null) return;

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var created = await users.TryAddAsync(new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = hasher.Hash(settings.AdminPassword),
            Role = Roles.Admin,
            CreatedAt = time.GetUtcNow(),
        }, ct);
        if (created) logger.LogInformation("Created the admin account {Email}", email);
    }

    private static string AdminEmailOf(AuthOptions settings) => settings.AdminEmail.Trim().ToLowerInvariant();
}
