namespace Nabadat.UserManagement.Domain.ValueObjects;

/// <summary>
/// The authenticated principal resolved from a session token. Returned by
/// <see cref="Interfaces.IUserManagementAuthService"/> and consumed by host middleware and
/// other modules as the request-scoped identity. Cross-module published type —
/// carries no persistence concerns.
/// </summary>
public sealed record SessionContext
{
    public required Guid SessionId { get; init; }

    public required Guid UserId { get; init; }

    /// <summary>Persona <c>P-01</c>..<c>P-08</c>.</summary>
    public required string Persona { get; init; }

    /// <summary>The effective permission snapshot for this session.</summary>
    public required PermissionSnapshot PermissionSnapshot { get; init; }
}
