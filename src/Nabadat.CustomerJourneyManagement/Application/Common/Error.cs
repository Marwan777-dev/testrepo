namespace Nabadat.CustomerJourneyManagement.Application.Common;

/// <summary>
/// A domain/application error carried by a failed <see cref="ServiceResult"/>. <see cref="Code"/>
/// is the stable, machine-readable identifier (e.g. <c>journey.name_conflict</c>) that the API
/// layer maps to the API-05 error envelope (<c>{ error: { code, message, … } }</c>) and an HTTP
/// status; <see cref="Message"/> is the human-readable detail.
/// </summary>
/// <param name="Code">Stable error code, dot-namespaced (e.g. <c>journey.name_conflict</c>).</param>
/// <param name="Message">Human-readable description of the failure.</param>
public sealed record Error(string Code, string Message);
