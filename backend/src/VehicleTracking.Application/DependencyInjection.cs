using Microsoft.Extensions.DependencyInjection;
using VehicleTracking.Application.Alerts;
using VehicleTracking.Application.Auth;
using VehicleTracking.Application.History;
using VehicleTracking.Application.Ingestion;
using VehicleTracking.Application.Live;
using VehicleTracking.Application.Status;
using VehicleTracking.Application.Transit;
using VehicleTracking.Application.TripUpdates;
using VehicleTracking.Application.Abstractions;

namespace VehicleTracking.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<SnapshotNormalizer>();
        services.AddSingleton<IngestionService>();
        services.AddSingleton<LiveVehicleService>();

        // One instance plays both roles: it holds the names, and it hears every live change to learn new ids.
        services.AddSingleton<ReferenceDataService>();
        services.AddSingleton<ILivePublisher>(provider => provider.GetRequiredService<ReferenceDataService>());
        services.AddSingleton<TripRouteService>();
        services.AddSingleton<AlertService>();
        services.AddSingleton<IAlertVersionReader>(provider => provider.GetRequiredService<AlertService>());
        services.AddSingleton<TripUpdateService>();
        services.AddSingleton<TripProgressService>();
        services.AddScoped<VehicleHistoryService>();
        services.AddScoped<LiveStateWarmUp>();
        services.AddScoped<SystemStatusService>();
        services.AddScoped<AuthService>();
        return services;
    }
}
