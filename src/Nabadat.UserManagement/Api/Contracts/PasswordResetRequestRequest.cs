namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>Request body for <c>POST /api/v1/auth/password-reset/request</c>.</summary>
public sealed record PasswordResetRequestRequest
{
    public string Email { get; init; } = string.Empty;
}
