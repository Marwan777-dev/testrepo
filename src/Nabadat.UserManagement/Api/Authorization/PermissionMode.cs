namespace Nabadat.UserManagement.Api.Authorization;

/// <summary>
/// The DOC-02 coarse access modes a module grant can hold, in ascending strength. A
/// <see cref="RequirePermissionAttribute"/> requires the actor's module grant
/// (<see cref="Domain.ValueObjects.PermissionSnapshot.Modules"/>) to <i>contain</i> the named mode —
/// grants are an explicit set, not a ladder, so requiring <see cref="Manage"/> does not accept a
/// grant that holds only <see cref="View"/>. Each member name is exactly the mode string stored in
/// the snapshot, resolved via <see cref="System.Enum.ToString()"/>.
/// </summary>
public enum PermissionMode
{
    /// <summary>Read access — list / view a module's entities.</summary>
    View,

    /// <summary>Create / edit access — the standard write mode for authoring personas.</summary>
    Manage,

    /// <summary>Full control, including destructive operations beyond <see cref="Manage"/>.</summary>
    Full,
}
