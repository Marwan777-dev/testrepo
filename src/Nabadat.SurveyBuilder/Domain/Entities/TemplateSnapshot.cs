namespace Nabadat.SurveyBuilder.Domain.Entities;

/// <summary>
/// The authoritative payload attached to a <see cref="Template"/> — a full copy of the source
/// survey's authoring state serialised as jsonb (tenant-schema table <c>template_snapshots</c>,
/// data-model.md §2.9). Keyed by <see cref="TemplateId"/> (1:1 with <c>templates</c>, ON DELETE
/// CASCADE); this table has no surrogate <c>id</c> or <c>row_version</c>.
/// <para><see cref="Snapshot"/> is the raw jsonb text — the Application layer
/// (<c>TemplateCommandService</c>) serialises/deserialises it to/from a
/// <c>SurveySnapshot</c> so the Domain stays free of Application types (dependency direction is
/// inward-only). <see cref="SchemaVersion"/> lets <c>TemplateInstantiator</c> migrate older
/// snapshots on read.</para>
/// </summary>
public sealed class TemplateSnapshot
{
    public Guid TemplateId { get; set; }

    /// <summary>Full authoring-state copy as jsonb text (the serialised <c>SurveySnapshot</c>).</summary>
    public string Snapshot { get; set; } = "{}";

    /// <summary>Snapshot schema version (>= 1). Default 1.</summary>
    public int SchemaVersion { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; }
}
