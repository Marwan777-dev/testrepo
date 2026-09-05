namespace Nabadat.SurveyBuilder.Domain.ValueObjects;

/// <summary>
/// The lifecycle state of a survey (tenant-schema column <c>surveys.status</c>, data-model.md
/// §2.1). Transitions are governed by the authoritative Survey Status Transition Matrix (spec.md,
/// BR-1.4) — see <see cref="SurveyStatusTransitions.AllowedTransitions"/>. Wire form is the
/// PascalCase member name.
/// </summary>
public enum SurveyStatus
{
    Draft,
    PendingReview,
    Active,
    Paused,
    Archived,
}
