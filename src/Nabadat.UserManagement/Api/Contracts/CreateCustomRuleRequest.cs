namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>Request body for <c>POST /api/v1/users/{userId}/custom-rules</c>.</summary>
public sealed record CreateCustomRuleRequest
{
    public IReadOnlyList<string> AllowedActions { get; init; } = [];

    public IReadOnlyDictionary<string, IReadOnlyList<string>> ParameterScopeAssignments { get; init; }
        = new Dictionary<string, IReadOnlyList<string>>();
}
