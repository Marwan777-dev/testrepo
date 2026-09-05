namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>A custom authorization rule as embedded in <see cref="UserScopeResponse"/>.</summary>
public sealed record CustomRuleDto
{
    public required Guid RuleId { get; init; }

    public IReadOnlyList<string> AllowedActions { get; init; } = [];

    public IReadOnlyDictionary<string, IReadOnlyList<string>> ParameterScopeAssignments { get; init; }
        = new Dictionary<string, IReadOnlyList<string>>();
}
