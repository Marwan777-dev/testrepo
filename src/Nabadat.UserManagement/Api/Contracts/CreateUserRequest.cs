namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>Request body for <c>POST /api/v1/users</c> — invite a new tenant user.</summary>
public sealed record CreateUserRequest
{
    public string Username { get; init; } = string.Empty;

    public string Persona { get; init; } = string.Empty;

    /// <summary>
    /// Initial password the admin sets for the new user (FR-027 complexity). The user
    /// signs in with it and enrols MFA on first login. Required (resolves gap I-01).
    /// </summary>
    public string Password { get; init; } = string.Empty;

    public Guid? OrganizationNodeId { get; init; }
}
