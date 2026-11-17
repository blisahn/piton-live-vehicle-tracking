using VehicleTracking.Api.Errors;
using VehicleTracking.Api.Health;
using VehicleTracking.Api.Realtime;
using VehicleTracking.Application;
using VehicleTracking.Application.Abstractions;
using VehicleTracking.Application.Configuration;
using VehicleTracking.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Invalid configuration stops the process at startup instead of failing on the first poll.
builder.Services.AddOptions<FeedOptions>()
    .BindConfiguration(FeedOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<AlertOptions>()
    .BindConfiguration(AlertOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<RetentionOptions>()
    .BindConfiguration(RetentionOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// One hub instance plays both roles: it receives live changes and it knows how many clients are connected.
builder.Services.AddSingleton<VehicleSocketHub>();
builder.Services.AddSingleton<ILivePublisher>(provider => provider.GetRequiredService<VehicleSocketHub>());
builder.Services.AddSingleton<IAlertPublisher>(provider => provider.GetRequiredService<VehicleSocketHub>());
builder.Services.AddSingleton(provider => new Lazy<IAlertVersionReader>(() => provider.GetRequiredService<IAlertVersionReader>()));
builder.Services.AddSingleton<IConnectionCounter>(provider => provider.GetRequiredService<VehicleSocketHub>());

// The browser app is served from another origin; only the configured origins may call the API or open a socket.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(cors => cors.AddDefaultPolicy(policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<RequestValidationExceptionHandler>();
builder.Services.AddExceptionHandler<TransitCatalogExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAppHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();

var webSockets = new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(15) };
foreach (var origin in allowedOrigins)
    webSockets.AllowedOrigins.Add(origin);
app.UseWebSockets(webSockets);

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapVehicleSocket();
app.MapAppHealthChecks();

app.Run();
