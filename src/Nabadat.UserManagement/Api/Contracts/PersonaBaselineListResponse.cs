namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>Response for <c>GET /api/v1/persona-baselines</c> — all persona baselines for the tenant.</summary>
public sealed record PersonaBaselineListResponse
{
    public required IReadOnlyList<PersonaBaselineResponse> Items { get; init; }
}
