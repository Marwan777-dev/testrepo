namespace Nabadat.SurveyBuilder.Domain.Entities;

/// <summary>
/// A survey section (tenant-schema table <c>sections</c>, data-model.md §2.2). Sections hold
/// standalone questions and questions-sets; <see cref="Order"/> is contiguous within a survey and
/// compacted on reorder (FR-8.2). The last section can be deleted (FR-2.3) — no minimum-count
/// invariant; the publish gate (BR-1.7) handles the "no sections" case separately.
/// </summary>
public sealed class Section
{
    public Guid Id { get; set; }

    /// <summary>Owning survey (intra-module FK, ON DELETE CASCADE).</summary>
    public Guid SurveyId { get; set; }

    /// <summary>Section name, 1–200 chars.</summary>
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Position within the survey — <c>(survey_id, order)</c> is unique.</summary>
    public int Order { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Monotonic ETag counter (research.md §2).</summary>
    public int RowVersion { get; set; } = 1;

    /// <summary>Bumps the ETag counter — call inside the write transaction on every mutation.</summary>
    public void IncrementRowVersion() => RowVersion++;
}
