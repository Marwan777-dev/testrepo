namespace Nabadat.UserManagement.Domain.Entities;

/// <summary>
/// Per-user fine-grained action and scope overrides beyond the persona baseline
/// (tenant-schema table <c>custom_authorization_rules</c>).
/// </summary>
public sealed class CustomAuthorizationRule
{
    public Guid RuleId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Fine-grained DOC-02 action codes (e.g. <c>UpdateSurvey</c>, <c>DeleteSurvey</c>).</summary>
    public IReadOnlyList<string> AllowedActions { get; set; } = [];

    /// <summary>Parameter scope grants, e.g. <c>{ "branch": ["Riyadh", "Dammam"] }</c>.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ParameterScopeAssignments { get; set; }
        = new Dictionary<string, IReadOnlyList<string>>();

    public Guid CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
