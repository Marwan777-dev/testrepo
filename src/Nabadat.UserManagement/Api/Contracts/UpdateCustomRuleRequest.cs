namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>Request body for <c>PUT /api/v1/users/{userId}/custom-rules/{ruleId}</c>.</summary>
public sealed record UpdateCustomRuleRequest
{
    public IReadOnlyList<string> AllowedActions { get; init; } = [];

    public IReadOnlyDictionary<string, IReadOnlyList<string>> ParameterScopeAssignments { get; init; }
        = new Dictionary<string, IReadOnlyList<string>>();
}
