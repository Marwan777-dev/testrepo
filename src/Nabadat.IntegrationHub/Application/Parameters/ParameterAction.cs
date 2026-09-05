namespace Nabadat.IntegrationHub.Application.Parameters;

/// <summary>
/// The closed set of operations <see cref="BuiltInParameterGuard"/> arbitrates (BR-09, <c>[PO-G27]</c>). Adding a
/// member forces a decision about its built-in policy — a field-set unit test pins the list so a new action
/// cannot silently default to "allowed".
/// </summary>
public enum ParameterAction
{
    /// <summary>Turn a parameter on. Allowed for both origins.</summary>
    Enable = 1,

    /// <summary>Turn a parameter off (guarded by BR-10's impact warning, not by this guard). Allowed for both origins.</summary>
    Disable = 2,

    /// <summary>Change the <c>snake_case</c> wire key. Never allowed on a built-in (BR-09/VR-F06).</summary>
    RenameApiField = 3,

    /// <summary>Change the data type. Never allowed on a built-in (<c>[PO-G27]</c>) → 409 <c>parameter.type_locked</c>.</summary>
    ChangeDataType = 4,

    /// <summary>Edit the bilingual display names. Allowed for both origins — BR-09's "never renamed" is about the API field.</summary>
    UpdateDisplayNames = 5,

    /// <summary>Edit the five usage flags. Allowed for both origins.</summary>
    UpdateUsageFlags = 6,

    /// <summary>
    /// Hard-delete. Never allowed for <b>either</b> origin (BR-09) — no <c>DELETE</c> endpoint exists at all;
    /// the guard is the second line of defence if one is ever wired by mistake.
    /// </summary>
    Delete = 7,
}
