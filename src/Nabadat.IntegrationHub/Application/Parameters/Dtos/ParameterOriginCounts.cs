namespace Nabadat.IntegrationHub.Application.Parameters.Dtos;

/// <summary>
/// SCR-05's origin-tab counts (FR-S5-01: "All · 23", "Built-in", "Custom").
///
/// <para>These are <b>global</b> — deliberately unaffected by the type filter and the search box (AC-S5-01: "the
/// tab counts stay global"). A tab whose count moved when a filter was applied would stop being a navigation
/// affordance and start being a second, contradictory result count.</para>
/// </summary>
/// <param name="All">Every parameter in the catalogue, enabled or not.</param>
/// <param name="BuiltIn">The seeded built-ins (23 after a fresh baseline, BR-23).</param>
/// <param name="Custom">Tenant-created parameters — the population VR-F13's 200 ceiling applies to.</param>
public sealed record ParameterOriginCounts(int All, int BuiltIn, int Custom);
