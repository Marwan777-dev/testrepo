using Nabadat.SurveyBuilder.Domain.Interfaces;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Surveys;

/// <summary>
/// The canonical M-17 audit payload for an approval-workflow action (FR-15.6). Carries the
/// constitution-registered <see cref="EventType"/> (AMENDMENT-012), the acting user, the correlation
/// id, the status transition (<see cref="PreviousStatus"/> → <see cref="NewStatus"/>), optional
/// reviewer <see cref="Remarks"/>, and the deterministic <see cref="OccurredAt"/> timestamp. Built by
/// <see cref="AuditEventFactory"/> and emitted via M-17's <c>IEventLogWriter</c> by the orchestrator
/// (T118).
/// </summary>
public sealed record ApprovalAuditEvent(
    string EventType,
    SurveyId Survey,
    Guid ActorId,
    Guid CorrelationId,
    SurveyStatus PreviousStatus,
    SurveyStatus NewStatus,
    string? Remarks,
    DateTimeOffset OccurredAt);
