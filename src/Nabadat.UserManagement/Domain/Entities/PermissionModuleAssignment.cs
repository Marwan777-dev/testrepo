namespace Nabadat.UserManagement.Domain.Entities;

/// <summary>
/// A user's access to a DOC-02 permission module (tenant-schema table
/// <c>permission_module_assignments</c>). Unique per <c>(user_id, module_id)</c>.
/// Permissions are indefinite until revoked (no effective-from/to in Phase 1).
/// </summary>
public sealed class PermissionModuleAssignment
{
    public Guid AssignmentId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Canonical DOC-02 module id (e.g. <c>SurveyBuilder</c>).</summary>
    public string ModuleId { get; set; } = string.Empty;

    /// <summary>Coarse modes: <c>View</c> | <c>Manage</c> | <c>Full</c> (per DOC-02 per module).</summary>
    public IReadOnlyList<string> AllowedModes { get; set; } = [];

    /// <summary>Actor who made the assignment.</summary>
    public Guid AssignedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
