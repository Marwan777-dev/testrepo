using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Surveys;

/// <summary>
/// The survey facts <see cref="PublishAuthorizationService"/> needs to authorize a publish: its
/// current <see cref="Status"/> and its author (<see cref="OwnerUserId"/>, compared to the caller
/// for the <c>PublishOwnSurveys</c> grant).
/// </summary>
public sealed record SurveyApprovalInfo(SurveyStatus Status, Guid OwnerUserId);
