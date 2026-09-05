using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>POST /api/v1/surveys/{id}/status body (contracts/surveys.md).</summary>
public sealed record SurveyStatusChangeRequest(SurveyStatus To, string? Reason = null, bool Confirm = false);
