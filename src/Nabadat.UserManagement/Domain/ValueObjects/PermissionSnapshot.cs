using System.Text.Json.Serialization;

namespace Nabadat.UserManagement.Domain.ValueObjects;

/// <summary>
/// Compact, serializable view of a user's effective permissions, stored in
/// <c>auth_sessions.permission_snapshot</c> (jsonb) and read in-process on every
/// permission check (AD-03 — no cache layer; the session row IS the snapshot).
///
/// Rebuilt when <c>tenant_users.last_permission_snapshot_version</c> no longer
/// matches the session's <see cref="Version"/>. <see cref="HierarchyDescendantIds"/>
/// is pre-computed from the materialized path so requests avoid a tree traversal.
/// </summary>
public sealed record PermissionSnapshot
{
    /// <summary>Snapshot version; matches the user's permission-snapshot version when built.</summary>
    [JsonPropertyName("version")]
    public long Version { get; init; }

    /// <summary>Module id → allowed coarse modes (e.g. <c>"SurveyBuilder": ["View","Manage"]</c>).</summary>
    [JsonPropertyName("modules")]
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Modules { get; init; }
        = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>Fine-grained DOC-02 action codes granted via custom rules (e.g. <c>UpdateSurvey</c>).</summary>
    [JsonPropertyName("customActions")]
    public IReadOnlyList<string> CustomActions { get; init; } = [];

    /// <summary>Parameter name → allowed values (e.g. <c>"branch": ["Riyadh","Dammam"]</c>).</summary>
    [JsonPropertyName("scopeAssignments")]
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ScopeAssignments { get; init; }
        = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>The user's assigned organization hierarchy node, if any.</summary>
    [JsonPropertyName("hierarchyNodeId")]
    public Guid? HierarchyNodeId { get; init; }

    /// <summary>Pre-computed descendant node ids of <see cref="HierarchyNodeId"/> (downward cascade).</summary>
    [JsonPropertyName("hierarchyDescendantIds")]
    public IReadOnlyList<Guid> HierarchyDescendantIds { get; init; } = [];
}
