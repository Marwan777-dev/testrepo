using Nabadat.UserManagement.Domain.ValueObjects;

namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>Response for <c>GET /api/v1/auth/session</c> — current session snapshot.</summary>
public sealed record SessionResponse
{
    public required Guid UserId { get; init; }

    public required string Persona { get; init; }

    public required PermissionSnapshot PermissionSnapshot { get; init; }
}
