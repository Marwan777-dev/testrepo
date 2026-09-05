namespace Nabadat.CustomerJourneyManagement.Domain.ValueObjects;

/// <summary>
/// Lifecycle state of a <see cref="Entities.Journey"/> (column <c>journeys.status</c>,
/// <c>varchar(16)</c>, default <c>Draft</c>). Lifecycle: <c>Draft</c> → <c>Active</c> ↔
/// <c>Inactive</c> → <c>Archived</c>; <c>Archived</c> is the terminal state.
/// <para>
/// Wire/storage form is the exact PascalCase member name (e.g. <c>"Draft"</c>) — entities
/// model the column as <see langword="string"/> (T008) and convert at the service boundary,
/// where the published-interface twin <see cref="Nabadat.Platform.Contracts.M16.JourneyConfigStatus"/>
/// carries the same members for M-06 consumption.
/// </para>
/// </summary>
public enum JourneyStatus
{
    /// <summary>Newly created; editable, not yet collecting responses (<c>Draft</c>).</summary>
    Draft,

    /// <summary>Live journey collecting and scoring responses (<c>Active</c>).</summary>
    Active,

    /// <summary>Temporarily paused; reactivatable to <see cref="Active"/> (<c>Inactive</c>).</summary>
    Inactive,

    /// <summary>Terminal, irreversible state; name released for reuse (<c>Archived</c>).</summary>
    Archived,
}
