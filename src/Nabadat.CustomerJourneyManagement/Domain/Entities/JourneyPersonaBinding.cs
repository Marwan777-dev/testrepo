namespace Nabadat.CustomerJourneyManagement.Domain.Entities;

/// <summary>
/// Many-to-many join between a journey and a bound persona (tenant-schema table
/// <c>journey_persona_bindings</c>, composite PK <c>(journey_id, persona_id)</c>). Only
/// <c>Active</c> personas can be bound — enforced at the service layer; unbinding is
/// always allowed.
/// </summary>
public sealed class JourneyPersonaBinding
{
    /// <summary>Bound journey (FK → <c>journeys.journey_id</c> ON DELETE CASCADE). Part of the composite PK.</summary>
    public Guid JourneyId { get; set; }

    /// <summary>Bound persona (FK → <c>personas.persona_id</c>). Part of the composite PK.</summary>
    public Guid PersonaId { get; set; }

    /// <summary>UTC timestamp when the binding was created.</summary>
    public DateTimeOffset BoundAt { get; set; }
}
