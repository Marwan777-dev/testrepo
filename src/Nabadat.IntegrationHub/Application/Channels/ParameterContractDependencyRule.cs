using Nabadat.IntegrationHub.Application.Channels.Dtos;

namespace Nabadat.IntegrationHub.Application.Channels;

/// <summary>
/// T032 — enforces FR-S4-04 / AC-S4-03: in a channel's parameter contract, <c>Required</c> may only be
/// <c>true</c> while <c>Supported</c> is <c>true</c>. Toggling <c>Supported</c> off force-clears
/// <c>Required</c> in the <b>same</b> write, so the persisted row can never violate the baseline's
/// <c>ck_channel_parameter_assignments_required_needs_supported</c> CHECK.
///
/// <para>This is a <b>normaliser, not a rejecter</b>. An inconsistent
/// <c>(supported=false, required=true)</c> pair is silently corrected rather than returned as a validation
/// error: SCR-04's dependency already prevents a user from producing it, and a stale client's contradiction
/// has exactly one safe resolution. Returning an error instead would block a legitimate save on a rule the
/// user cannot see.</para>
///
/// <para>FR-S4-03's live contract-summary counts are derived from the normalised rows, which is why
/// normalisation runs before both counting and persistence.</para>
/// </summary>
public sealed class ParameterContractDependencyRule
{
    /// <summary>Normalises one row's flag pair.</summary>
    public ContractFlags Apply(bool supported, bool required) =>
        new(supported, supported && required);

    /// <summary>
    /// Normalises a whole submitted contract, preserving the submitted row order (SCR-04 renders the
    /// contract in catalogue order and the response should not reshuffle it). A <c>null</c> or empty input
    /// yields an empty contract — a channel that supports nothing yet is legal.
    /// </summary>
    public IReadOnlyList<ChannelParameterAssignmentInput> ApplyAll(
        IEnumerable<ChannelParameterAssignmentInput>? rows)
    {
        if (rows is null)
        {
            return Array.Empty<ChannelParameterAssignmentInput>();
        }

        return rows
            .Select(row =>
            {
                var flags = Apply(row.Supported, row.Required);
                return row with { Supported = flags.Supported, Required = flags.Required };
            })
            .ToList();
    }
}
