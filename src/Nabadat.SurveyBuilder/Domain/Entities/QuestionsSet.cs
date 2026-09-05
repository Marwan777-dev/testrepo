using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Domain.Entities;

/// <summary>
/// A rotating question pool inside a section (tenant-schema table <c>questions_sets</c>,
/// data-model.md §2.3, F10). A set delivers <see cref="Count"/> of its member questions per
/// respondent per dispatch, chosen by <see cref="SelectionMode"/>. <c>count</c> is bounded
/// <c>0 &lt;= count &lt;= size(set)</c> — the floor is a DB CHECK, the ceiling is enforced by
/// <c>QuestionsSetValidator</c> (cross-row). Set questions cannot be routing sources or targets
/// (FR-9.5). <see cref="Order"/> positions the set alongside standalone questions within the section.
/// </summary>
public sealed class QuestionsSet
{
    public Guid Id { get; set; }

    /// <summary>Owning section (FK, ON DELETE CASCADE).</summary>
    public Guid SectionId { get; set; }

    /// <summary>Set title, 1–200 chars.</summary>
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Random vs prioritise-low-response subset selection (default Random).</summary>
    public QuestionsSetSelectionMode SelectionMode { get; set; } = QuestionsSetSelectionMode.Random;

    /// <summary>Questions delivered per respondent per dispatch (<c>0 &lt;= count &lt;= size(set)</c>).</summary>
    public int Count { get; set; }

    /// <summary>Position within the section (alongside standalone questions).</summary>
    public int Order { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Monotonic ETag counter (research.md §2).</summary>
    public int RowVersion { get; set; } = 1;

    /// <summary>Bumps the ETag counter — call inside the write transaction on every mutation.</summary>
    public void IncrementRowVersion() => RowVersion++;
}
