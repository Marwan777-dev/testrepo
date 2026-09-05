namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>Request body for <c>PUT /api/v1/persona-baselines/{personaId}</c>.</summary>
public sealed record UpdatePersonaBaselineRequest
{
    public IReadOnlyList<ModuleAssignmentDto> PermissionModuleAssignments { get; init; } = [];
}
