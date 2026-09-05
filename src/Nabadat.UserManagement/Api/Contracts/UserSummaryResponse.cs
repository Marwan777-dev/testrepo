namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>A user row as returned by <c>GET /api/v1/users</c> and write-endpoint responses.</summary>
public sealed record UserSummaryResponse
{
    public required Guid UserId { get; init; }

    public required string Username { get; init; }

    public required string Persona { get; init; }

    public required string Status { get; init; }

    public required bool IsMfaEnrolled { get; init; }

    public Guid? OrganizationNodeId { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}
