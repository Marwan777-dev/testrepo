namespace Nabadat.IntegrationHub.Domain.Entities;

/// <summary>
/// The channel contract row (data-model.md §5): which parameters a channel's backend may send
/// (<see cref="Supported"/>) and which of those are mandatory (<see cref="Required"/>). <b>This table —
/// not <see cref="Parameter.RequiredByDefault"/> — is the authority on requiredness at request
/// time</b> (BR-08).
///
/// <para>Composite PK <c>(service_channel_id, parameter_id)</c>. Permitted because neither half is a
/// tenant identifier (DB-03 forbids only tenant-identifier composites).</para>
/// </summary>
public sealed class ChannelParameterAssignment
{
    /// <summary>Intra-module FK → <see cref="ServiceChannel"/>; part of the composite PK.</summary>
    public Guid ServiceChannelId { get; set; }

    /// <summary>Intra-module FK → <see cref="Parameter"/>; part of the composite PK.</summary>
    public Guid ParameterId { get; set; }

    public bool Supported { get; set; }

    /// <summary>
    /// May only be <c>true</c> while <see cref="Supported"/> is <c>true</c> (FR-S4-04) — enforced
    /// server-side and by a DB CHECK: clearing <see cref="Supported"/> force-clears this in the same
    /// write. Seeded from <see cref="Parameter.RequiredByDefault"/> when the parameter is first assigned
    /// (FR-S6-05), then independently editable per channel.
    /// </summary>
    public bool Required { get; set; }
}
