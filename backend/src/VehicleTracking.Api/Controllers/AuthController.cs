using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using VehicleTracking.Api.Auth;
using VehicleTracking.Api.Contracts;
using VehicleTracking.Application.Auth;
using VehicleTracking.Application.Configuration;

namespace VehicleTracking.Api.Controllers;

/// <summary>Registration and sign-in. The access token is in the body, the refresh token only in an HttpOnly cookie.</summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService auth, IOptions<AuthOptions> options, FailedSignInLimiter failedSignIns) : ControllerBase
{
    internal const string RefreshCookie = "refresh_token";
    internal const string CookiePath = "/api/auth";
    internal const string RegistrationRateLimit = "auth-registration";
    internal const string RefreshRateLimit = "auth-refresh";

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RegistrationRateLimit)]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<AuthResponseDto>> Register(CredentialsRequest request, CancellationToken cancellationToken)
    {
        var result = await auth.RegisterAsync(request.Email, request.Password, cancellationToken);
        SetRefreshCookie(result);
        return StatusCode(StatusCodes.Status201Created, AuthResponseDto.From(result));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResponseDto>> Login(CredentialsRequest request, CancellationToken cancellationToken)
    {
        var address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (failedSignIns.IsBlocked(address))
            return Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Too many requests",
                detail: "Too many failed sign-ins. Try again later.");

        try
        {
            var result = await auth.LoginAsync(request.Email, request.Password, cancellationToken);
            SetRefreshCookie(result);
            return AuthResponseDto.From(result);
        }
        catch (InvalidCredentialsException)
        {
            failedSignIns.RecordFailure(address);
            throw;
        }
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RefreshRateLimit)]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResponseDto>> Refresh(CancellationToken cancellationToken)
    {
        try
        {
            var result = await auth.RefreshAsync(Request.Cookies[RefreshCookie], cancellationToken);
            SetRefreshCookie(result);
            return AuthResponseDto.From(result);
        }
        catch (InvalidRefreshTokenException)
        {
            ClearRefreshCookie();
            throw;
        }
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await auth.LogoutAsync(Request.Cookies[RefreshCookie], cancellationToken);
        ClearRefreshCookie();
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDto>> Me(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var id)) return Unauthorized();
        var user = await auth.GetUserAsync(id, cancellationToken);
        return user is null ? Unauthorized() : UserDto.From(user);
    }

    private CookieOptions CookieOptions() => new()
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Strict,
        Secure = options.Value.CookieSecure,
        Path = CookiePath,
    };

    private void SetRefreshCookie(AuthResult result)
    {
        var cookie = CookieOptions();
        cookie.MaxAge = options.Value.RefreshTokenLifetime;
        Response.Cookies.Append(RefreshCookie, result.RefreshToken, cookie);
    }

    private void ClearRefreshCookie() => Response.Cookies.Delete(RefreshCookie, CookieOptions());
}
