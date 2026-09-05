using Nabadat.IntegrationHub.Domain.Entities;
using Nabadat.IntegrationHub.Domain.ValueObjects;

namespace Nabadat.IntegrationHub.Application.Parameters;

/// <summary>
/// T053 — enforces BR-11 / FR-S6-02: a parameter's API field name is editable until the first inbound request
/// carrying it has been received, then locked permanently — renaming after that would break the caller (tenet
/// T-08). Built-ins are <b>always</b> locked (BR-09).
///
/// <para>The lock has three independent sources, OR-ed together:</para>
/// <list type="number">
///   <item>the persisted one-way <see cref="Parameter.ApiFieldLocked"/> flag, set by US4's request pipeline;</item>
///   <item>a live "has a request carried this field?" probe the caller passes in — defence in depth for the case
///   where traffic exists but the flag was never written; and</item>
///   <item><see cref="ParameterOrigin.BuiltIn"/> — a built-in stays locked even if its seeded flag were somehow
///   cleared, so BR-09 does not depend on a single boolean column staying correct.</item>
/// </list>
///
/// <para>Deliberately shaped like <c>ChannelIdLockGuard</c> (T031) so the module's two lock rules read the same.
/// The guard is pure: <see cref="ParameterService"/> resolves the probe and passes a boolean, which keeps the
/// enforcement <b>server-side</b> — a stale client that still renders the field editable cannot get around it.</para>
///
/// <para><see cref="Parameter.Enabled"/> and this lock are <b>independent axes</b> (spec.md Edge Cases):
/// re-enabling a disabled parameter never unlocks its field name.</para>
/// </summary>
public sealed class ApiFieldNameLockGuard
{
    /// <summary>
    /// True when the parameter's API field name may no longer change — the persisted flag is set, the caller's
    /// probe found a request carrying the field, or the parameter is a built-in.
    /// </summary>
    public bool IsLocked(Parameter parameter, bool hasReceivedRequest)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        return parameter.ApiFieldLocked
            || hasReceivedRequest
            || parameter.Origin == ParameterOrigin.BuiltIn;
    }

    /// <summary>
    /// Validates an attempted API-field change against the lock.
    ///
    /// <para>A <c>null</c> <paramref name="requestedApiField"/> means the client did not submit the field (which
    /// a locked parameter's read-only form does) — not a change, so it is valid. A submitted value equal to the
    /// persisted one is likewise no change, which is what lets a display-name edit, a usage-flag change, or an
    /// enable/disable toggle still save on a locked parameter. Because the request pipeline matches the wire key
    /// exactly, a case-only difference is a real change and is rejected.</para>
    /// </summary>
    public ParameterValidationResult ValidateApiFieldChange(
        Parameter parameter,
        bool hasReceivedRequest,
        string? requestedApiField)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        if (requestedApiField is null
            || string.Equals(requestedApiField, parameter.ApiField, StringComparison.Ordinal))
        {
            return ParameterValidationResult.Valid;
        }

        return IsLocked(parameter, hasReceivedRequest)
            ? ParameterValidationResult.Invalid(new ParameterValidationError(
                ParameterErrorCodes.ApiFieldLocked,
                "The API field name is locked once the first request using it has been received and can no longer be changed",
                ParameterFields.ApiField))
            : ParameterValidationResult.Valid;
    }
}
