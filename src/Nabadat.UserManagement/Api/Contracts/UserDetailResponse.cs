namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>
/// Response for <c>GET /api/v1/users/{userId}</c> — profile plus the user's permission
/// module assignments. Custom authorization rules and data-scope assignments are
/// surfaced by the data-scope story (US3); this response carries the module grants
/// the permissions editor needs in US2.
/// </summary>
public sealed record UserDetailResponse
{
    public required Guid UserId { get; init; }

    public required string Username { get; init; }

    public required string Persona { get; init; }

    public required string Status { get; init; }

    public required bool IsMfaEnrolled { get; init; }

    public Guid? OrganizationNodeId { get; init; }

    public required long LastPermissionSnapshotVersion { get; init; }

    public required IReadOnlyList<ModuleAssignmentDto> PermissionModuleAssignments { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}
