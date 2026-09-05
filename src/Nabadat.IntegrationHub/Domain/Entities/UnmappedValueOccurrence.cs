namespace Nabadat.IntegrationHub.Domain.Entities;

/// <summary>
/// One raw value seen on an inbound request for a mapping-enabled <see cref="Parameter"/> that has no
/// matching <see cref="ParameterMapping"/> — the backing row for SCR-07's trailing-7-day
/// unmapped-values queue (FR-S7-02, data-model.md §7).
///
/// <para><b>Why a table and not a live query:</b> <see cref="IntegrationRequestLog"/> is high-volume
/// and partitioned, so computing the queue from it on every page load would be expensive. This table is
/// small and purpose-built.</para>
///
/// <para><b>Lifecycle:</b> a row is created on first sighting and its <see cref="LastSeenAt"/> /
/// <see cref="OccurrenceCount"/> updated on repeats; it is deleted once a mapping exists for that
/// <c>(parameter_id, lower(raw_value))</c> pair. Rows older than 7 days by
/// <see cref="FirstSeenAt"/> drop out of the queue view (a repeat occurrence does not reset that
/// window).</para>
/// </summary>
public sealed class UnmappedValueOccurrence
{
    public Guid Id { get; set; }

    /// <summary>Intra-module FK → <see cref="Parameter"/>.</summary>
    public Guid ParameterId { get; set; }

    /// <summary>
    /// Case-preserved as received, but matched case-insensitively against
    /// <see cref="ParameterMapping.SourceValue"/> to decide whether it is now mapped (VR-F08).
    /// </summary>
    public string RawValue { get; set; } = string.Empty;

    /// <summary>Drives the 7-day queue window.</summary>
    public DateTimeOffset FirstSeenAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    /// <summary>Informational only — it does not affect queue membership or ordering guarantees.</summary>
    public int OccurrenceCount { get; set; } = 1;
}
