namespace Nabadat.SurveyBuilder.Domain.Interfaces;

/// <summary>
/// The tenant design-guideline tokens M-01 resolves for a survey in <c>Inherited</c> appearance mode
/// (F4, research.md §4.3). Read from M-11 via <see cref="ITenantDesignGuidelinesReader"/>. Only the
/// tokens M-01 renders are surfaced; the full token set is finalised with M-11 when it ships.
/// </summary>
/// <param name="PrimaryColour">Primary brand colour (hex).</param>
/// <param name="TextColour">Body text colour (hex).</param>
/// <param name="ButtonRadiusPx">Default button corner radius.</param>
public sealed record TenantDesignGuidelines(
    string PrimaryColour,
    string? TextColour = null,
    int? ButtonRadiusPx = null);
