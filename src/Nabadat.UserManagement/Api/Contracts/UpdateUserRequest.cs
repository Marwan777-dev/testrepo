namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>Request body for <c>PUT /api/v1/users/{userId}</c> — update profile.</summary>
public sealed record UpdateUserRequest
{
    /// <summary>New persona (P-01..P-08). Only P-01 actors may change a user's persona.</summary>
    public string? Persona { get; init; }

    /// <summary>New organization node, or <c>null</c> to clear the scope.</summary>
    public Guid? OrganizationNodeId { get; init; }
}
