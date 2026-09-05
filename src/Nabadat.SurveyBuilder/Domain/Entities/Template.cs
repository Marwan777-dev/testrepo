using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Domain.Entities;

/// <summary>
/// A survey template — the metadata row parallel to a survey, paired 1:1 with a
/// <see cref="TemplateSnapshot"/> that holds the full authoring-state copy (tenant-schema table
/// <c>templates</c>, data-model.md §2.8, Q4/BR-7.1 snapshot-no-link). <see cref="Class"/> decides
/// editability (FR-7.1): <see cref="TemplateClass.BuiltIn"/> rows are system-authored
/// (<see cref="CreatedBy"/>/<see cref="UpdatedBy"/> null, no <see cref="Tags"/>) and locked;
/// <see cref="TemplateClass.Customized"/> rows are tenant-authored (no <see cref="Sectors"/>).
/// </summary>
public sealed class Template
{
    public Guid Id { get; set; }

    /// <summary><see cref="TemplateClass.BuiltIn"/> (locked) | <see cref="TemplateClass.Customized"/> (editable).</summary>
    public TemplateClass Class { get; set; } = TemplateClass.Customized;

    /// <summary>English template name — required.</summary>
    public string NameEn { get; set; } = string.Empty;

    public string? NameAr { get; set; }

    public string? Description { get; set; }

    /// <summary>Customized templates carry tags (F6 tag search); BuiltIn is empty (its facet is <see cref="Sectors"/>).</summary>
    public string[] Tags { get; set; } = Array.Empty<string>();

    /// <summary>BuiltIn only — Banking, Telecom, Government, … (F6 sector filter). Empty for Customized.</summary>
    public string[] Sectors { get; set; } = Array.Empty<string>();

    /// <summary>Optional file-storage handle for the F6 preview card image.</summary>
    public string? PreviewThumbnailFileHandle { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Null for <see cref="TemplateClass.BuiltIn"/> (system-authored).</summary>
    public Guid? CreatedBy { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    /// <summary>Monotonic ETag counter (research.md §2). Default 1; bumped on every write.</summary>
    public int RowVersion { get; set; } = 1;

    /// <summary>Bumps the ETag counter — call inside the write transaction on every mutation.</summary>
    public void IncrementRowVersion() => RowVersion++;
}
