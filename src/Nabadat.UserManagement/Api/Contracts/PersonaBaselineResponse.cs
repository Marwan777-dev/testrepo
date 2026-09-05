namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>A persona baseline row in <c>GET /api/v1/persona-baselines</c>.</summary>
public sealed record PersonaBaselineResponse
{
    public required Guid BaselineId { get; init; }

    public required string PersonaId { get; init; }

    public required IReadOnlyList<ModuleAssignmentDto> PermissionModuleAssignments { get; init; }

    public required IReadOnlyDictionary<string, IReadOnlyList<string>> DefaultDataScopeRules { get; init; }

    public required bool IsCustomised { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}
