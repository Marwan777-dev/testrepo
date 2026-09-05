namespace Nabadat.CustomerJourneyManagement.Application.Events;

/// <summary>
/// The canonical <c>event_type</c> string for every M-16 audit event written to
/// M-17's <c>event_log</c>. These are the 15 events registered for module M-16 in
/// the platform constitution (v1.8.0, AMENDMENT-007). Use these constants instead
/// of magic strings so a typo can never reach the wire (<c>event_log.event_type</c>
/// is <c>varchar(64)</c>; every value below fits).
/// </summary>
public static class CustomerJourneyManagementEventTypes
{
    // Journey lifecycle & structure (12)
    public const string JourneyCreated = "journey.created";
    public const string JourneyUpdated = "journey.updated";
    public const string JourneyStatusChanged = "journey.status.changed";
    public const string JourneyStageAdded = "journey.stage.added";
    public const string JourneyStageRemoved = "journey.stage.removed";
    public const string JourneyTouchpointAdded = "journey.touchpoint.added";
    public const string JourneyTouchpointRemoved = "journey.touchpoint.removed";
    public const string JourneyKpiBindingsUpdated = "journey.kpi_bindings.updated";
    public const string JourneyScoringConfigUpdated = "journey.scoring_config.updated";
    public const string JourneyDetectionConfigUpdated = "journey.detection_config.updated";
    public const string JourneyVersionPublished = "journey.version.published";
    public const string JourneyScoreUpdated = "journey.score.updated";

    // Persona lifecycle (3)
    public const string PersonaCreated = "persona.created";
    public const string PersonaUpdated = "persona.updated";
    public const string PersonaStatusChanged = "persona.status.changed";

    /// <summary>All 15 M-16 event-type strings, in registry order.</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        JourneyCreated,
        JourneyUpdated,
        JourneyStatusChanged,
        JourneyStageAdded,
        JourneyStageRemoved,
        JourneyTouchpointAdded,
        JourneyTouchpointRemoved,
        JourneyKpiBindingsUpdated,
        JourneyScoringConfigUpdated,
        JourneyDetectionConfigUpdated,
        JourneyVersionPublished,
        JourneyScoreUpdated,
        PersonaCreated,
        PersonaUpdated,
        PersonaStatusChanged,
    };
}
