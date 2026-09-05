namespace Nabadat.CustomerJourneyManagement.Domain.ValueObjects;

/// <summary>
/// Lifecycle state of a <see cref="Entities.Persona"/> (column <c>personas.status</c>,
/// <c>varchar(16)</c>, default <c>Draft</c>). Lifecycle: <c>Draft</c> → <c>Active</c> ↔
/// <c>Inactive</c> → <c>Archived</c>; <c>Archived</c> is terminal and a persona may not be
/// archived while it has active journey bindings.
/// <para>
/// Only <see cref="Active"/> personas appear in the journey binding selector. Wire/storage
/// form is the exact PascalCase member name; entities model the column as <see langword="string"/>
/// (T008) and convert at the service boundary.
/// </para>
/// </summary>
public enum PersonaStatus
{
    /// <summary>Newly created; editable, not yet bindable to journeys (<c>Draft</c>).</summary>
    Draft,

    /// <summary>Published; eligible for journey binding (<c>Active</c>).</summary>
    Active,

    /// <summary>Temporarily withdrawn from the binding selector; reactivatable (<c>Inactive</c>).</summary>
    Inactive,

    /// <summary>Terminal, irreversible state; blocked while active bindings exist (<c>Archived</c>).</summary>
    Archived,
}
