using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.Configuration;
using VehicleTracking.Infrastructure.Entur;
using VehicleTracking.Infrastructure.LiveState;
using VehicleTracking.Infrastructure.Metrics;
using VehicleTracking.Infrastructure.Persistence;
using VehicleTracking.Infrastructure.Spool;
using VehicleTracking.Infrastructure.Transit;
using VehicleTracking.Infrastructure.Workers;

namespace VehicleTracking.Infrastructure;

public static class DependencyInjection
{
    /// <summary>A read that takes longer than this is abandoned; the next poll is the retry.</summary>
    private static readonly TimeSpan FeedTimeout = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan JourneyPlannerTimeout = TimeSpan.FromSeconds(10);

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SpoolOptions>()
            .BindConfiguration(SpoolOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<IFeedSource, EnturFeedSource>((provider, http) =>
        {
            var feed = provider.GetRequiredService<IOptions<FeedOptions>>().Value;
            http.Timeout = FeedTimeout;
            http.DefaultRequestHeaders.Add("ET-Client-Name", feed.ClientName);
        });

        services.AddHttpClient<IAlertFeedSource, EnturAlertFeedSource>((provider, http) =>
        {
            var feed = provider.GetRequiredService<IOptions<FeedOptions>>().Value;
            http.Timeout = FeedTimeout;
            http.DefaultRequestHeaders.Add("ET-Client-Name", feed.ClientName);
        });

        services.AddOptions<JourneyPlannerOptions>()
            .BindConfiguration(JourneyPlannerOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<ITransitCatalog, EnturJourneyPlannerCatalog>((provider, http) =>
        {
            var feed = provider.GetRequiredService<IOptions<FeedOptions>>().Value;
            http.Timeout = JourneyPlannerTimeout;
            http.DefaultRequestHeaders.Add("ET-Client-Name", feed.ClientName);
        });

        services.AddSingleton<ILiveState, InMemoryLiveState>();
        services.AddSingleton<IIngestionMetrics, IngestionMetrics>();
        services.AddSingleton<IPositionSpool, FilePositionSpool>();

        var connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("Connection string 'Database' is not configured.");
        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.AddDbContext<TrackingDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>()));

        services.AddSingleton<DatabaseReadiness>();
        services.AddSingleton<IPositionWriter, NpgsqlPositionWriter>();
        services.AddSingleton<IReferenceDataStore, NpgsqlReferenceDataStore>();
        services.AddScoped<IPositionHistoryReader, EfPositionHistoryReader>();
        services.AddScoped<IIngestStatsReader, EfIngestStatsReader>();
        services.AddScoped<ILatestPositionReader, EfLatestPositionReader>();

        services.AddHostedService<DatabaseInitializer>();
        services.AddHostedService<FeedPollingWorker>();
        services.AddHostedService<AlertPollingWorker>();
        services.AddHostedService<PositionWriterWorker>();
        services.AddHostedService<RetentionWorker>();
        services.AddHostedService<ReferenceDataWorker>();
        return services;
    }
}
