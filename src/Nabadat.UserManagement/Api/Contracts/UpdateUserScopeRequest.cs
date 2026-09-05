namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>
/// Request body for <c>PUT /api/v1/users/{userId}/scope</c> — replaces the user's
/// hierarchy node and parameter scope assignments (<c>organizationNodeId</c> may be null
/// to clear the hierarchy scope).
/// </summary>
public sealed record UpdateUserScopeRequest
{
    public Guid? OrganizationNodeId { get; init; }

    public IReadOnlyList<DataScopeAssignmentDto> DataScopeAssignments { get; init; } = [];
}
