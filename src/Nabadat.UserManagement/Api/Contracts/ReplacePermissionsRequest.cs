namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>Request body for <c>PUT /api/v1/users/{userId}/permissions</c> — full replace.</summary>
public sealed record ReplacePermissionsRequest
{
    public IReadOnlyList<ModuleAssignmentDto> Assignments { get; init; } = [];
}
