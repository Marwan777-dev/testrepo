namespace Nabadat.CustomerJourneyManagement.Application.Events;

/// <summary>
/// An auditable M-16 event, published to M-17's <c>event_log</c> in the same
/// transaction as the action that produced it (FR-015). <see cref="OldValue"/> /
/// <see cref="NewValue"/> are serialized to <c>jsonb</c>; <c>null</c> means
/// "not applicable" (e.g. no prior value on a create).
/// <para>
/// Prefer the typed factory helpers (<see cref="JourneyCreated"/>, …) over the
/// initializer: each pins the correct <see cref="EventType"/> and a sensible
/// <see cref="EntityType"/>, so callers cannot mismatch the two.
/// </para>
/// </summary>
public sealed record CustomerJourneyManagementEvent
{
    /// <summary>One of <see cref="CustomerJourneyManagementEventTypes"/>.</summary>
    public required string EventType { get; init; }

    public required Guid ActorId { get; init; }

    /// <summary>Actor persona <c>P-01</c>..<c>P-08</c>.</summary>
    public required string ActorPersona { get; init; }

    /// <summary>Logical entity kind, e.g. <c>journey</c>, <c>stage</c>, <c>persona</c>.</summary>
    public required string EntityType { get; init; }

    public required Guid EntityId { get; init; }

    public object? OldValue { get; init; }

    public object? NewValue { get; init; }

    public required DateTimeOffset OccurredAtUtc { get; init; }

    public required Guid CorrelationId { get; init; }

    // --- Entity-kind tags (event_log.entity_type, varchar(128)) -----------------
    private const string EntityJourney = "journey";
    private const string EntityStage = "stage";
    private const string EntityTouchpoint = "touchpoint";
    private const string EntityJourneyVersion = "journey_version";
    private const string EntityJourneyScore = "journey_score";
    private const string EntityScoringConfig = "scoring_config";
    private const string EntityPersona = "persona";

    private static CustomerJourneyManagementEvent Create(
        string eventType,
        string entityType,
        Guid actorId,
        string actorPersona,
        Guid entityId,
        DateTimeOffset occurredAtUtc,
        Guid correlationId,
        object? newValue,
        object? oldValue) => new()
        {
            EventType = eventType,
            EntityType = entityType,
            ActorId = actorId,
            ActorPersona = actorPersona,
            EntityId = entityId,
            OccurredAtUtc = occurredAtUtc,
            CorrelationId = correlationId,
            NewValue = newValue,
            OldValue = oldValue,
        };

    // --- Typed publish helpers (one per M-16 event type) ------------------------

    public static CustomerJourneyManagementEvent JourneyCreated(Guid actorId, string actorPersona, Guid journeyId, DateTimeOffset occurredAtUtc, Guid correlationId, object? newValue = null) =>
        Create(CustomerJourneyManagementEventTypes.JourneyCreated, EntityJourney, actorId, actorPersona, journeyId, occurredAtUtc, correlationId, newValue, oldValue: null);

    public static CustomerJourneyManagementEvent JourneyUpdated(Guid actorId, string actorPersona, Guid journeyId, DateTimeOffset occurredAtUtc, Guid correlationId, object? newValue = null, object? oldValue = null) =>
        Create(CustomerJourneyManagementEventTypes.JourneyUpdated, EntityJourney, actorId, actorPersona, journeyId, occurredAtUtc, correlationId, newValue, oldValue);

    public static CustomerJourneyManagementEvent JourneyStatusChanged(Guid actorId, string actorPersona, Guid journeyId, DateTimeOffset occurredAtUtc, Guid correlationId, object? newValue = null, object? oldValue = null) =>
        Create(CustomerJourneyManagementEventTypes.JourneyStatusChanged, EntityJourney, actorId, actorPersona, journeyId, occurredAtUtc, correlationId, newValue, oldValue);

    public static CustomerJourneyManagementEvent JourneyStageAdded(Guid actorId, string actorPersona, Guid stageId, DateTimeOffset occurredAtUtc, Guid correlationId, object? newValue = null) =>
        Create(CustomerJourneyManagementEventTypes.JourneyStageAdded, EntityStage, actorId, actorPersona, stageId, occurredAtUtc, correlationId, newValue, oldValue: null);

    public static CustomerJourneyManagementEvent JourneyStageRemoved(Guid actorId, string actorPersona, Guid stageId, DateTimeOffset occurredAtUtc, Guid correlationId, object? oldValue = null) =>
        Create(CustomerJourneyManagementEventTypes.JourneyStageRemoved, EntityStage, actorId, actorPersona, stageId, occurredAtUtc, correlationId, newValue: null, oldValue);

    public static CustomerJourneyManagementEvent JourneyTouchpointAdded(Guid actorId, string actorPersona, Guid touchpointId, DateTimeOffset occurredAtUtc, Guid correlationId, object? newValue = null) =>
        Create(CustomerJourneyManagementEventTypes.JourneyTouchpointAdded, EntityTouchpoint, actorId, actorPersona, touchpointId, occurredAtUtc, correlationId, newValue, oldValue: null);

    public static CustomerJourneyManagementEvent JourneyTouchpointRemoved(Guid actorId, string actorPersona, Guid touchpointId, DateTimeOffset occurredAtUtc, Guid correlationId, object? oldValue = null) =>
        Create(CustomerJourneyManagementEventTypes.JourneyTouchpointRemoved, EntityTouchpoint, actorId, actorPersona, touchpointId, occurredAtUtc, correlationId, newValue: null, oldValue);

    public static CustomerJourneyManagementEvent JourneyKpiBindingsUpdated(Guid actorId, string actorPersona, Guid journeyId, DateTimeOffset occurredAtUtc, Guid correlationId, object? newValue = null, object? oldValue = null) =>
        Create(CustomerJourneyManagementEventTypes.JourneyKpiBindingsUpdated, EntityJourney, actorId, actorPersona, journeyId, occurredAtUtc, correlationId, newValue, oldValue);

    /// <summary>
    /// Tenant-level scoring-config edit (SRS §4.2.9 / §11.7, Q11). <paramref name="scoringConfigId"/> is
    /// the single per-tenant <c>scoring_configs</c> row's id (entity kind <c>scoring_config</c>) — there
    /// is no journey scope.
    /// </summary>
    public static CustomerJourneyManagementEvent JourneyScoringConfigUpdated(Guid actorId, string actorPersona, Guid scoringConfigId, DateTimeOffset occurredAtUtc, Guid correlationId, object? newValue = null, object? oldValue = null) =>
        Create(CustomerJourneyManagementEventTypes.JourneyScoringConfigUpdated, EntityScoringConfig, actorId, actorPersona, scoringConfigId, occurredAtUtc, correlationId, newValue, oldValue);

    public static CustomerJourneyManagementEvent JourneyDetectionConfigUpdated(Guid actorId, string actorPersona, Guid journeyId, DateTimeOffset occurredAtUtc, Guid correlationId, object? newValue = null, object? oldValue = null) =>
        Create(CustomerJourneyManagementEventTypes.JourneyDetectionConfigUpdated, EntityJourney, actorId, actorPersona, journeyId, occurredAtUtc, correlationId, newValue, oldValue);

    public static CustomerJourneyManagementEvent JourneyVersionPublished(Guid actorId, string actorPersona, Guid versionId, DateTimeOffset occurredAtUtc, Guid correlationId, object? newValue = null) =>
        Create(CustomerJourneyManagementEventTypes.JourneyVersionPublished, EntityJourneyVersion, actorId, actorPersona, versionId, occurredAtUtc, correlationId, newValue, oldValue: null);

    public static CustomerJourneyManagementEvent JourneyScoreUpdated(Guid actorId, string actorPersona, Guid journeyId, DateTimeOffset occurredAtUtc, Guid correlationId, object? newValue = null, object? oldValue = null) =>
        Create(CustomerJourneyManagementEventTypes.JourneyScoreUpdated, EntityJourneyScore, actorId, actorPersona, journeyId, occurredAtUtc, correlationId, newValue, oldValue);

    public static CustomerJourneyManagementEvent PersonaCreated(Guid actorId, string actorPersona, Guid personaId, DateTimeOffset occurredAtUtc, Guid correlationId, object? newValue = null) =>
        Create(CustomerJourneyManagementEventTypes.PersonaCreated, EntityPersona, actorId, actorPersona, personaId, occurredAtUtc, correlationId, newValue, oldValue: null);

    public static CustomerJourneyManagementEvent PersonaUpdated(Guid actorId, string actorPersona, Guid personaId, DateTimeOffset occurredAtUtc, Guid correlationId, object? newValue = null, object? oldValue = null) =>
        Create(CustomerJourneyManagementEventTypes.PersonaUpdated, EntityPersona, actorId, actorPersona, personaId, occurredAtUtc, correlationId, newValue, oldValue);

    public static CustomerJourneyManagementEvent PersonaStatusChanged(Guid actorId, string actorPersona, Guid personaId, DateTimeOffset occurredAtUtc, Guid correlationId, object? newValue = null, object? oldValue = null) =>
        Create(CustomerJourneyManagementEventTypes.PersonaStatusChanged, EntityPersona, actorId, actorPersona, personaId, occurredAtUtc, correlationId, newValue, oldValue);
}
