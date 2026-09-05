using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Surveys.Dtos;

/// <summary>
/// Command for a self-serve survey status transition (Pause / Reactivate / Archive / Unarchive /
/// destructive Return-to-Draft) orchestrated by <c>SurveyLifecycleService</c> (T073). <see cref="Confirm"/>
/// acknowledges a destructive or rules-gated transition (BR-1.6, FR-1.10).
/// </summary>
public sealed record SurveyStatusChangeCommand(
    Guid SurveyId,
    SurveyStatus TargetStatus,
    Guid ActorId,
    string ActorRole,
    Guid CorrelationId,
    bool Confirm = false);
