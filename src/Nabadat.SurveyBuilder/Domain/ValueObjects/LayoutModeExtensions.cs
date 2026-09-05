namespace Nabadat.SurveyBuilder.Domain.ValueObjects;

/// <summary>
/// Behaviour for <see cref="LayoutMode"/> (kept in its own file per the one-type-per-file
/// convention, mirroring <c>IdentityProviderTypeExtensions</c>).
/// </summary>
public static class LayoutModeExtensions
{
    /// <summary>
    /// Whether the mode needs a <c>questions_per_page</c> value. Only <see cref="LayoutMode.Count"/>
    /// (a set number per page) does; the survey invariant requires <c>questions_per_page ≥ 1</c>
    /// in that case (data-model.md §2.1).
    /// </summary>
    public static bool RequiresQuestionsPerPage(this LayoutMode mode) => mode == LayoutMode.Count;
}
