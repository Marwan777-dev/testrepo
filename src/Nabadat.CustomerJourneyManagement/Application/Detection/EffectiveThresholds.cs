namespace Nabadat.CustomerJourneyManagement.Application.Detection;

/// <summary>
/// The resolved (never-null) pain/happy detection threshold pair for a single scope, produced by
/// <see cref="DetectionOverrideResolver.GetEffectiveThresholdsAsync"/> after walking the
/// most-specific-wins chain (touchpoint &gt; stage &gt; journey default — research.md §5). A score
/// at or below <see cref="PainThreshold"/> is a pain point; at or above <see cref="HappyThreshold"/>
/// a happy moment; the band between them is neutral. The invariant
/// <c>PainThreshold &lt; HappyThreshold</c> is guaranteed by the save-time validation in
/// <c>DetectionConfigService</c> (T085), so resolution never has to re-check it.
/// </summary>
/// <param name="PainThreshold">Score ≤ this value = pain point.</param>
/// <param name="HappyThreshold">Score ≥ this value = happy moment.</param>
public sealed record EffectiveThresholds(decimal PainThreshold, decimal HappyThreshold);
