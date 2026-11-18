using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using VehicleTracking.Application.Auth;

namespace VehicleTracking.Api.Errors;

/// <summary>Turns sign-in failures raised in the Application layer into 401 and 409 problem responses.</summary>
internal sealed class AuthExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            EmailAlreadyRegisteredException => (StatusCodes.Status409Conflict, "Email already registered"),
            InvalidCredentialsException => (StatusCodes.Status401Unauthorized, "Sign-in failed"),
            InvalidRefreshTokenException => (StatusCodes.Status401Unauthorized, "Session expired"),
            _ => (0, string.Empty),
        };
        if (status == 0) return false;

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = exception.Message },
        });
    }
}
