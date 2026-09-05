namespace Nabadat.UserManagement.Domain.ValueObjects;

/// <summary>
/// Coarse-grained access mode for a DOC-02 permission module. The exact set of
/// modes valid for a given module is defined by DOC-02; this enum is the union.
/// Stored on the wire as the PascalCase name (e.g. <c>Manage</c>) inside the
/// <c>allowed_modes varchar[]</c> column.
/// </summary>
public enum PermissionMode
{
    /// <summary>Read-only access to the module.</summary>
    View,

    /// <summary>Create/update access within the module.</summary>
    Manage,

    /// <summary>Full control including configuration and destructive actions.</summary>
    Full,
}
