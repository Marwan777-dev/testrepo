namespace Nabadat.UserManagement.Application.Permissions.Exceptions;

/// <summary>
/// A single field-level validation failure, mapped to the API-05 error envelope's
/// <c>details</c> array (<c>{ "field": ..., "code": ... }</c>).
/// </summary>
public sealed record ValidationFailure(string Field, string Code);
