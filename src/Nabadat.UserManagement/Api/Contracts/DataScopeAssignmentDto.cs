namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>
/// A parameter scope grant on the wire — used in both scope requests and responses
/// (e.g. <c>{ "parameterName": "branch", "allowedValues": ["Riyadh", "Dammam"] }</c>).
/// </summary>
public sealed record DataScopeAssignmentDto
{
    public string ParameterName { get; init; } = string.Empty;

    public IReadOnlyList<string> AllowedValues { get; init; } = [];
}
