namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// Body of <c>POST /api/v1/surveys/{id}/routing</c> — the F9 survey-level routing toggle
/// (contracts/questions.md). Enabling requires <see cref="Confirm"/> = true (FR-9.1 confirmation
/// modal); enabling also disables and locks shuffle (LayoutRoutingCoupler).
/// </summary>
/// <param name="Enabled">Target routing state.</param>
/// <param name="Confirm">Client acknowledgement of the FR-9.1 confirmation; required to enable.</param>
public sealed record EnableRoutingRequest(bool Enabled, bool Confirm);
