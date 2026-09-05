namespace Nabadat.SurveyBuilder.Domain.Interfaces;

/// <summary>
/// An M-01 audit/domain event handed to the M-17 <see cref="IEventLogWriter"/> (data-model.md §7).
/// The M-01-published event types are <c>survey.published</c> (any transition into Active),
/// <c>survey.archived</c> (into Archived), <c>survey.created</c>, <c>survey.status.changed</c>, and
/// <c>survey.submitted_for_review</c> (constitution AMENDMENT-012 / T022). <see cref="Payload"/>
/// carries the event-specific fields (e.g. <c>from_status</c>, <c>to_status</c>,
/// <c>purged_response_count</c>).
/// </summary>
/// <param name="EventType">Dot-namespaced event type, e.g. <c>survey.published</c>.</param>
/// <param name="SurveyId">The survey the event is about.</param>
/// <param name="ActorId">M-10 user id of the actor.</param>
/// <param name="CorrelationId">Correlation id tying sub-actions of one operation together.</param>
/// <param name="Payload">Event-specific fields.</param>
public sealed record SurveyAuditEvent(
    string EventType,
    Guid SurveyId,
    Guid ActorId,
    Guid CorrelationId,
    IReadOnlyDictionary<string, object?> Payload)
{
    /// <summary>Convenience factory with an empty payload.</summary>
    public static SurveyAuditEvent Create(string eventType, Guid surveyId, Guid actorId, Guid correlationId) =>
        new(eventType, surveyId, actorId, correlationId, new Dictionary<string, object?>());
}
