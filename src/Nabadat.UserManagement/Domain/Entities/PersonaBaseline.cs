namespace Nabadat.UserManagement.Domain.Entities;

/// <summary>
/// Per-tenant default permission module assignments for a persona (control-plane
/// table <c>persona_baselines</c>). Control-plane entity — carries an explicit
/// <see cref="TenantId"/> (DB-02 exemption). Seeded P-01..P-08 at provisioning.
/// </summary>
public sealed class PersonaBaseline
{
    public Guid BaselineId { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>Persona id <c>P-01</c>..<c>P-08</c>.</summary>
    public string PersonaId { get; set; } = string.Empty;

    /// <summary>
    /// Default module grants for this persona, e.g.
    /// <c>[{ "moduleId": "SurveyBuilder", "allowedModes": ["View","Manage"] }]</c>.
    /// Stored as jsonb.
    /// </summary>
    public IReadOnlyList<PersonaModuleAssignment> PermissionModuleAssignments { get; set; } = [];

    /// <summary>Default data-scope rules for this persona (open jsonb shape).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> DefaultDataScopeRules { get; set; }
        = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>True once a tenant admin has modified the platform default.</summary>
    public bool IsCustomised { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
