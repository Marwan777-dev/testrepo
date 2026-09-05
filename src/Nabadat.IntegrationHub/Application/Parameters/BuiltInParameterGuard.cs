using Nabadat.IntegrationHub.Application.Parameters.Exceptions;

namespace Nabadat.IntegrationHub.Application.Parameters;

/// <summary>
/// T056 — enforces BR-09 / <c>[PO-G27]</c>: the 23 seeded built-in parameters may only be enabled, disabled,
/// re-labelled, and have their usage flags changed. Renaming a built-in's API field or changing its data type is
/// rejected, and <b>no</b> parameter of either origin may be hard-deleted.
///
/// <para>Unlike the accumulating validators, this guard <b>throws</b>. That is deliberate: a rejected action here
/// is not a correctable field error the drawer renders inline — it is an operation the product does not offer, so
/// there is nothing to accumulate and no message to attach to an input. The thrown
/// <see cref="BuiltInParameterViolationException"/> carries the code the controller maps to 409.</para>
/// </summary>
public sealed class BuiltInParameterGuard
{
    /// <summary>
    /// Throws when <paramref name="action"/> is not permitted on a parameter of the given origin; returns
    /// silently otherwise.
    /// </summary>
    /// <exception cref="BuiltInParameterViolationException">
    /// The action is a hard delete (either origin), or a rename/type change on a built-in.
    /// </exception>
    public void Guard(bool builtIn, ParameterAction action)
    {
        // BR-09 applies the delete prohibition to BOTH origins: customs are disabled, never hard-deleted.
        if (action == ParameterAction.Delete)
        {
            throw new BuiltInParameterViolationException(
                ParameterErrorCodes.ParameterNotFound,
                "Parameters are disabled, never deleted — no delete operation exists");
        }

        if (!builtIn)
        {
            return;
        }

        switch (action)
        {
            case ParameterAction.RenameApiField:
                throw new BuiltInParameterViolationException(
                    ParameterErrorCodes.ApiFieldLocked,
                    "A built-in parameter's API field name is permanently read-only");

            case ParameterAction.ChangeDataType:
                throw new BuiltInParameterViolationException(
                    ParameterErrorCodes.ParameterTypeLocked,
                    "A built-in parameter's data type is read-only");

            // Enable / Disable / UpdateDisplayNames / UpdateUsageFlags are the four operations BR-09 leaves open
            // on a built-in. Listed explicitly so adding an enum member is a compile-time decision, not a silent
            // fall-through into "allowed".
            case ParameterAction.Enable:
            case ParameterAction.Disable:
            case ParameterAction.UpdateDisplayNames:
            case ParameterAction.UpdateUsageFlags:
            default:
                return;
        }
    }
}
