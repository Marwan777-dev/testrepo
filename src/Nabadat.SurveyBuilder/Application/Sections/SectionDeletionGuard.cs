namespace Nabadat.SurveyBuilder.Application.Sections;

/// <summary>
/// Min-count guard for section deletion (T138). FR-2.3 is explicit: the <b>last</b> section can be
/// deleted — there is no minimum-count invariant, so this always allows deletion. The "no sections"
/// case is handled separately by the publish gate (BR-1.7), not here. Pure.
/// </summary>
public sealed class SectionDeletionGuard
{
    /// <summary>Always <c>true</c> — sections have no minimum-count invariant (FR-2.3).</summary>
    public bool CanDelete(int sectionCountInSurvey) => true;
}
