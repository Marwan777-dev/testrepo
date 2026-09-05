namespace Nabadat.UserManagement.Application.Permissions;

/// <summary>
/// One parameter definition inside an inbound <see cref="M13ParameterPayload"/>:
/// a scope parameter name, its display label, and the full set of valid values.
/// </summary>
public sealed record M13ParameterDefinition
{
    public required string Name { get; init; }

    public required string Label { get; init; }

    public required IReadOnlyList<string> AllowedValues { get; init; }
}
