using DotNet.Testcontainers.Configurations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;
using VehicleTracking.Application;
using VehicleTracking.Application.Configuration;
using VehicleTracking.Infrastructure;
using VehicleTracking.Infrastructure.Persistence;

namespace VehicleTracking.IntegrationTests;

/// <summary>
/// One TimescaleDB container for all integration tests, set up exactly the way the app does it:
/// the production service registrations, and the schema created by <see cref="DatabaseInitializer"/>
/// (migrations, hypertable, chunk interval derived from the retention period).
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    /// <summary>The same image as docker-compose.yml.</summary>
    private const string Image = "timescale/timescaledb:2.17.2-pg16";

    /// <summary>Short enough that a chunk is one minute (retention / 12), so retention can be tested in seconds.</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromMinutes(12);

    private PostgreSqlContainer? _container;

    public ServiceProvider Services { get; private set; } = null!;

    public NpgsqlDataSource DataSource => Services.GetRequiredService<NpgsqlDataSource>();

    public async Task InitializeAsync()
    {
        if (!DockerAvailable.IsAvailable) return;

        _container = new PostgreSqlBuilder(Image)
            .WithDatabase("vehicle_tracking")
            .WithUsername("vehicle_tracking")
            .WithPassword("integration-tests")
            .Build();
        await _container.StartAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = _container.GetConnectionString(),
                ["Retention:Period"] = Retention.ToString(),
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddOptions<RetentionOptions>().BindConfiguration(RetentionOptions.SectionName);
        services.AddOptions<AuthOptions>().BindConfiguration(AuthOptions.SectionName);
        services.AddApplication();
        services.AddInfrastructure(configuration);
        Services = services.BuildServiceProvider();

        // Run only the initializer, not the feed pollers and other workers.
        var initializer = ActivatorUtilities.CreateInstance<DatabaseInitializer>(Services);
        await initializer.StartAsync(CancellationToken.None);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await Services.GetRequiredService<DatabaseReadiness>().WaitAsync(timeout.Token);
        await initializer.StopAsync(CancellationToken.None);
    }

    /// <summary>Empties every table a test writes to, so tests do not see each other's rows.</summary>
    public async Task ResetAsync()
    {
        await using var cmd = DataSource.CreateCommand(
            "truncate vehicle_positions; truncate ingest_stats; truncate refresh_tokens, users cascade;");
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var cmd = DataSource.CreateCommand(sql);
        foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value);
        return (T)Convert.ChangeType((await cmd.ExecuteScalarAsync())!, typeof(T));
    }

    public async Task DisposeAsync()
    {
        if (Services is not null) await Services.DisposeAsync();
        if (_container is not null) await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "database";
}

/// <summary>
/// These tests need Docker. Without it they are skipped, not failed, so `dotnet test` still works
/// on a machine that only has the .NET SDK.
/// </summary>
public static class DockerAvailable
{
    public static readonly bool IsAvailable = Detect();

    private static bool Detect()
    {
        try
        {
            return TestcontainersSettings.OS.DockerEndpointAuthConfig is not null;
        }
        catch
        {
            return false;
        }
    }
}

public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        if (!DockerAvailable.IsAvailable) Skip = "Docker is not available; integration tests need it.";
    }
}

/// <summary>Base for tests that share the database: each test starts with empty tables.</summary>
[Collection(DatabaseCollection.Name)]
public abstract class DatabaseTest(DatabaseFixture db) : IAsyncLifetime
{
    protected DatabaseFixture Db { get; } = db;

    public Task InitializeAsync() => DockerAvailable.IsAvailable ? Db.ResetAsync() : Task.CompletedTask;

    public Task DisposeAsync() => Task.CompletedTask;
}
