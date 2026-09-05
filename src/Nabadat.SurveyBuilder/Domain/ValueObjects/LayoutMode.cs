namespace Nabadat.SurveyBuilder.Domain.ValueObjects;

/// <summary>
/// Survey page layout (tenant-schema column <c>surveys.layout</c>, data-model.md §2.1, F3).
/// <list type="bullet">
///   <item><see cref="Single"/> — all questions on one page.</item>
///   <item><see cref="Section"/> — one page per section (default).</item>
///   <item><see cref="Question"/> — one question per page (required when routing is on).</item>
///   <item><see cref="Count"/> — a set number of questions per page (needs
///   <c>questions_per_page</c>).</item>
/// </list>
/// See <see cref="LayoutModeExtensions.RequiresQuestionsPerPage"/>.
/// </summary>
public enum LayoutMode
{
    Single,
    Section,
    Question,
    Count,
}
