namespace Nabadat.SurveyBuilder.Application.Surveys;

/// <summary>
/// Result of the BR-1.7 publish content gate (<c>PublishGateService.EnsureContent</c>, T070).
/// <see cref="Gated"/> is false when the transition is not a gated entry into Active (e.g. Paused →
/// Active reactivation, Q9); when gated and unsatisfied, <see cref="ErrorCode"/> is
/// <c>publish.requires_content</c> and the missing-* flags say which invariant failed.
/// </summary>
public sealed record PublishGateResult(
    bool Gated,
    bool IsSatisfied,
    string? ErrorCode,
    bool MissingSections,
    bool MissingQuestions)
{
    /// <summary>The transition is not content-gated (e.g. Paused → Active) — always satisfied.</summary>
    public static PublishGateResult NotGated() => new(false, true, null, false, false);

    /// <summary>Gated and satisfied — both content invariants hold.</summary>
    public static PublishGateResult Satisfied() => new(true, true, null, false, false);

    /// <summary>Gated and rejected — <c>publish.requires_content</c> with the failing invariants.</summary>
    public static PublishGateResult Rejected(bool missingSections, bool missingQuestions) =>
        new(true, false, "publish.requires_content", missingSections, missingQuestions);
}
