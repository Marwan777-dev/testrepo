namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>
/// A persisted permission module grant on the wire — returned in the
/// <c>PUT /api/v1/users/{userId}/permissions</c> 200 response so the client sees the
/// server-assigned <see cref="AssignmentId"/> for each surviving assignment.
/// </summary>
public sealed record PermissionAssignmentDto
{
    public Guid AssignmentId { get; init; }

    public string ModuleId { get; init; } = string.Empty;

    public IReadOnlyList<string> AllowedModes { get; init; } = [];
}
