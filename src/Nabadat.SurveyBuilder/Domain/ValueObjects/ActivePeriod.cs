namespace Nabadat.SurveyBuilder.Domain.ValueObjects;

/// <summary>
/// How long a survey keeps collecting after it is sent (tenant-schema column
/// <c>surveys.active_period</c>, data-model.md §2.1, FR-3.4). Serialised to jsonb as
/// <c>{"days": int, "hours": int}</c> by a value converter (T062). A <c>null</c> ActivePeriod on
/// the survey means it never auto-expires (FR-3.4) — represented by the column being NULL, not by
/// a zero-valued instance.
/// </summary>
/// <param name="Days">Whole days of the active window; must be ≥ 0.</param>
/// <param name="Hours">Additional hours of the active window; must be ≥ 0.</param>
public sealed record ActivePeriod(int Days, int Hours)
{
    /// <summary>Total duration, useful for expiry arithmetic (start + this = expiry).</summary>
    public TimeSpan ToTimeSpan() => new(Days, Hours, minutes: 0, seconds: 0);

    /// <summary>
    /// A period is valid when both components are non-negative and it is not entirely zero
    /// (a zero-length window would expire instantly; callers wanting "no expiry" use a NULL
    /// column instead).
    /// </summary>
    public static bool IsValid(ActivePeriod period) =>
        period.Days >= 0 && period.Hours >= 0 && (period.Days > 0 || period.Hours > 0);
}
