using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nabadat.UserManagement.Api.Contracts;
using Nabadat.UserManagement.Api.Interfaces;
using Nabadat.UserManagement.Application.Auth;
using Nabadat.UserManagement.Application.Auth.Dtos;
using Nabadat.UserManagement.Application.Auth.Exceptions;
using Nabadat.UserManagement.Application.Auth.Interfaces;

namespace Nabadat.UserManagement.Api.Controllers;

/// <summary>
/// Authentication endpoints (auth-api.md): login → MFA enroll/verify → session, plus
/// logout and self-service password reset. Non-2xx responses use the API-05 envelope.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly TenantAuthenticationService _authentication;
    private readonly MfaChallengeValidator _mfaChallenge;
    private readonly MfaEnrollmentService _mfaEnrollment;
    private readonly PasswordResetService _passwordReset;
    private readonly ISessionService _sessions;
    private readonly ISessionContextAccessor _sessionContext;
    private readonly IMfaChallengeService _challenges;

    public AuthController(
        TenantAuthenticationService authentication,
        MfaChallengeValidator mfaChallenge,
        MfaEnrollmentService mfaEnrollment,
        PasswordResetService passwordReset,
        ISessionService sessions,
        ISessionContextAccessor sessionContext,
        IMfaChallengeService challenges)
    {
        _authentication = authentication;
        _mfaChallenge = mfaChallenge;
        _mfaEnrollment = mfaEnrollment;
        _passwordReset = passwordReset;
        _sessions = sessions;
        _sessionContext = sessionContext;
        _challenges = challenges;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        CredentialValidationResult result;
        try
        {
            result = await _authentication.ValidateCredentialsAsync(request.Username, request.Password, ct);
        }
        catch (AccountLockedException)
        {
            return Error(StatusCodes.Status423Locked, "auth.account_locked", "The account is locked.");
        }

        if (result.Outcome == CredentialOutcome.InvalidCredentials)
        {
            return Error(StatusCodes.Status401Unauthorized, "auth.invalid_credentials", "Invalid username or password.");
        }

        return Ok(new LoginResponse
        {
            ChallengeId = result.ChallengeId!,
            RequiresMfaEnrollment = result.Outcome == CredentialOutcome.RequiresMfaEnrollment,
        });
    }

    [HttpPost("mfa/enroll")]
    public async Task<IActionResult> EnrollMfa([FromBody] MfaEnrollRequest request, CancellationToken ct)
    {
        try
        {
            var initiation = await _mfaEnrollment.InitiateEnrollmentAsync(request.ChallengeId, ct);
            return Ok(new MfaEnrollResponse
            {
                OtpauthUri = initiation.OtpauthUri,
                Base32Secret = initiation.Base32Secret,
                EnrollmentToken = initiation.EnrollmentToken,
            });
        }
        catch (MfaValidationException)
        {
            return Error(StatusCodes.Status400BadRequest, "auth.mfa.invalid_challenge", "The challenge is invalid or expired.");
        }
    }

    [HttpPost("mfa/enroll/confirm")]
    public async Task<IActionResult> ConfirmMfaEnrollment([FromBody] MfaEnrollConfirmRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _mfaEnrollment.ConfirmEnrollmentAsync(request.EnrollmentToken, request.TotpCode, ct);
            return Ok(ToSessionToken(result));
        }
        catch (MfaValidationException)
        {
            return Error(StatusCodes.Status422UnprocessableEntity, "auth.mfa.invalid_code", "The MFA code is invalid.");
        }
    }

    [HttpPost("mfa/verify")]
    public async Task<IActionResult> VerifyMfa([FromBody] MfaVerifyRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _mfaChallenge.VerifyAsync(request.ChallengeId, request.TotpCode, ct);
            return Ok(ToSessionToken(result));
        }
        catch (AccountLockedException)
        {
            return Error(StatusCodes.Status423Locked, "auth.account_locked", "The account is locked.");
        }
        catch (MfaValidationException)
        {
            return Error(StatusCodes.Status422UnprocessableEntity, "auth.mfa.invalid_code", "The MFA code is invalid.");
        }
    }

    [HttpPost("mfa/skip")]
    public async Task<IActionResult> SkipMfa([FromBody] MfaSkipRequest request, CancellationToken ct)
    {
        var challenge = _challenges.ResolveChallenge(request.ChallengeId);
        if (challenge is null)
        {
            return Error(StatusCodes.Status400BadRequest, "auth.mfa.invalid_challenge", "The challenge is invalid or expired.");
        }

        var session = await _sessions.CreateSessionAsync(challenge.UserId, ct);
        _challenges.ConsumeChallenge(request.ChallengeId);

        return Ok(new SessionTokenResponse
        {
            SessionToken = session.RawToken,
            UserId = challenge.UserId,
            ExpiresAtUtc = session.Session.AbsoluteExpiresAtUtc,
            PermissionSnapshot = session.Session.PermissionSnapshot,
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var session = _sessionContext.Current;
        if (session is not null)
        {
            await _sessions.InvalidateSessionAsync(session.SessionId, ct);
        }

        return NoContent();
    }

    [HttpPost("password-reset/request")]
    public async Task<IActionResult> RequestPasswordReset([FromBody] PasswordResetRequestRequest request, CancellationToken ct)
    {
        try
        {
            await _passwordReset.RequestResetAsync(request.Email, ct);
            return Accepted();
        }
        catch (PasswordResetRateLimitExceededException ex)
        {
            Response.Headers.RetryAfter = ex.RetryAfterSeconds.ToString();
            return Error(StatusCodes.Status429TooManyRequests, "auth.password_reset.rate_limited", "Too many reset requests.");
        }
        catch (Exception)
        {
            // M-09 delivery failed; token was rolled back and the request is safe to retry.
            return Error(StatusCodes.Status503ServiceUnavailable, "auth.password_reset.delivery_unavailable", "Reset delivery is temporarily unavailable.");
        }
    }

    [HttpPost("password-reset/redeem")]
    public async Task<IActionResult> RedeemPasswordReset([FromBody] PasswordResetRedeemRequest request, CancellationToken ct)
    {
        try
        {
            var requiresReenrollment = await _passwordReset.RedeemResetAsync(request.Token, request.NewPassword, ct);
            return Ok(new PasswordResetRedeemResponse { RequiresMfaReenrollment = requiresReenrollment });
        }
        catch (Exception ex) when (ex is TokenExpiredException or TokenAlreadyUsedException or TokenRevokedException)
        {
            return Error(StatusCodes.Status400BadRequest, "auth.password_reset.invalid_token", "The reset token is invalid.");
        }
        catch (WeakPasswordException)
        {
            return Error(StatusCodes.Status422UnprocessableEntity, "auth.password_reset.weak_password", "Password does not meet complexity requirements.");
        }
    }

    [HttpGet("session")]
    public IActionResult GetSession()
    {
        var session = _sessionContext.Current;
        if (session is null)
        {
            return Error(StatusCodes.Status401Unauthorized, "auth.session_invalid", "Session expired or invalid.");
        }

        return Ok(new SessionResponse
        {
            UserId = session.UserId,
            Persona = session.Persona,
            PermissionSnapshot = session.PermissionSnapshot,
        });
    }

    private static SessionTokenResponse ToSessionToken(MfaChallengeResult result) => new()
    {
        SessionToken = result.SessionToken,
        UserId = result.UserId,
        ExpiresAtUtc = result.ExpiresAtUtc,
        PermissionSnapshot = result.PermissionSnapshot,
    };

    private ObjectResult Error(int status, string code, string message) => StatusCode(status, new ApiErrorEnvelope
    {
        Error = new ApiErrorDetail
        {
            Code = code,
            Message = message,
            CorrelationId = HttpContext.TraceIdentifier,
        },
    });
}
