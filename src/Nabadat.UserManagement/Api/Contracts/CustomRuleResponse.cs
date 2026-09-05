namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>
/// Response for create/update of a custom authorization rule
/// (<c>POST</c>/<c>PUT /api/v1/users/{userId}/custom-rules</c>).
/// </summary>
public sealed record CustomRuleResponse
{
    public required Guid RuleId { get; init; }

    public IReadOnlyList<string> AllowedActions { get; init; } = [];

    public IReadOnlyDictionary<string, IReadOnlyList<string>> ParameterScopeAssignments { get; init; }
        = new Dictionary<string, IReadOnlyList<string>>();

    public required DateTimeOffset CreatedAt { get; init; }
}
