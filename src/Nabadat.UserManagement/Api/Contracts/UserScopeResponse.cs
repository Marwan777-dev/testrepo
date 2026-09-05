namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>
/// Response for <c>GET /api/v1/users/{userId}/scope</c> — the user's hierarchy node,
/// parameter scope assignments, and custom authorization rules.
/// </summary>
public sealed record UserScopeResponse
{
    public Guid? OrganizationNodeId { get; init; }

    public IReadOnlyList<DataScopeAssignmentDto> DataScopeAssignments { get; init; } = [];

    public IReadOnlyList<CustomRuleDto> CustomRules { get; init; } = [];
}
