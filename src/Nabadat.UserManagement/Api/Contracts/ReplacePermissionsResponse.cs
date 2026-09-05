namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>
/// 200 response body for <c>PUT /api/v1/users/{userId}/permissions</c> — the full set
/// of assignments that survived the replace (users-api.md).
/// </summary>
public sealed record ReplacePermissionsResponse
{
    public IReadOnlyList<PermissionAssignmentDto> Assignments { get; init; } = [];
}
