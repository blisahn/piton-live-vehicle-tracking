using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using VehicleTracking.Api.Controllers;
using VehicleTracking.Api.Realtime;
using VehicleTracking.Application.Configuration;
using VehicleTracking.Infrastructure.Auth;

namespace VehicleTracking.Api.Auth;

internal static class AuthSetup
{
    public static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    public static IServiceCollection AddAppAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // The key is shared with the token issuer, so the options are completed once the container can supply it.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtSigningKey, IOptions<AuthOptions>>((bearer, signingKey, auth) =>
            {
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = auth.Value.Issuer,
                    ValidateAudience = true,
                    ValidAudience = auth.Value.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey.Key,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true,
                    ClockSkew = ClockSkew,
                    NameClaimType = "email",
                    RoleClaimType = "role",
                };

                // Browsers cannot set headers on a WebSocket, so the socket carries the token in the query string.
                bearer.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (context.HttpContext.Request.Path.StartsWithSegments(VehicleSocketEndpoint.Path)
                            && context.Request.Query.TryGetValue("access_token", out var token)
                            && !string.IsNullOrEmpty(token))
                            context.Token = token;
                        return Task.CompletedTask;
                    },
                };
            });

        // Anything not marked otherwise needs a signed-in user, so a forgotten endpoint is closed, not open.
        services.AddAuthorization(o =>
            o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        return services;
    }

    public static IServiceCollection AddAuthRateLimit(this IServiceCollection services)
    {
        services.AddSingleton<FailedSignInLimiter>();
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // Separate budgets per client address. Sign-ups are limited against scripted account creation;
            // refreshes happen on every page load and tab, so sharing a budget would log active users out.
            // Sign-ins are limited by FailedSignInLimiter, which counts failures only.
            limiter.AddPolicy(AuthController.RegistrationRateLimit, context =>
            {
                var auth = context.RequestServices.GetRequiredService<IOptions<AuthOptions>>().Value;
                return RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = auth.RegistrationsPerWindow,
                        Window = auth.RegistrationWindow,
                        QueueLimit = 0,
                    });
            });
            limiter.AddPolicy(AuthController.RefreshRateLimit, context =>
            {
                var auth = context.RequestServices.GetRequiredService<IOptions<AuthOptions>>().Value;
                return RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = auth.RefreshesPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    });
            });
            limiter.OnRejected = async (rejected, cancellationToken) =>
            {
                var http = rejected.HttpContext;
                http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    http.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
                await http.RequestServices.GetRequiredService<IProblemDetailsService>().TryWriteAsync(
                    new ProblemDetailsContext
                    {
                        HttpContext = http,
                        ProblemDetails = new ProblemDetails
                        {
                            Status = StatusCodes.Status429TooManyRequests,
                            Title = "Too many requests",
                            Detail = "Too many attempts. Try again later.",
                        },
                    });
            };
        });
        return services;
    }
}
