namespace Nabadat.IntegrationHub.Domain.ValueObjects;

/// <summary>
/// Whether a <see cref="Entities.Parameter"/> ships with the platform or was authored by the tenant
/// (data-model.md §4). Origin is the enforcement axis for BR-09: built-ins may only be
/// enabled/disabled — never deleted, never renamed, and their data type is read-only
/// (<c>[PO-G27]</c>, which is why <c>Parameter.DataTypeLocked</c> is derived from this and not
/// separately stored). Custom parameters may be disabled but are never hard-deleted either.
/// <para>Only <see cref="Custom"/> rows count toward VR-F13's ≤200-custom-parameter tenant ceiling.
/// Persisted as <c>built_in</c> / <c>custom</c> via <c>ParameterOriginConverter</c>.</para>
/// </summary>
public enum ParameterOrigin
{
    /// <summary>One of the 23 normative built-ins seeded at tenant creation, all enabled (FR-F0-10, BR-23).</summary>
    BuiltIn = 1,

    /// <summary>A tenant-authored parameter created through SCR-06.</summary>
    Custom = 2,
}
