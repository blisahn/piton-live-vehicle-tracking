using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using VehicleTracking.Application.Abstractions;

namespace VehicleTracking.Api.Errors;

/// <summary>Turns a failed lookup in the journey planner into a 503 problem response.</summary>
internal sealed class TransitCatalogExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not TransitCatalogException) return false;

        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Transit catalog unavailable",
                Detail = exception.Message,
            },
        });
    }
}
