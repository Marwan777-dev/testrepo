namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>Response for a successful <c>PUT /api/v1/persona-baselines/{personaId}</c>.</summary>
public sealed record UpdatePersonaBaselineResponse
{
    public required Guid BaselineId { get; init; }

    public required string PersonaId { get; init; }

    public required bool IsCustomised { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}
