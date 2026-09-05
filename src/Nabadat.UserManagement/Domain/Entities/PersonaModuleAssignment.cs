namespace Nabadat.UserManagement.Domain.Entities;

/// <summary>A single module grant inside a <see cref="PersonaBaseline"/> jsonb payload.</summary>
public sealed record PersonaModuleAssignment
{
    public string ModuleId { get; init; } = string.Empty;

    public IReadOnlyList<string> AllowedModes { get; init; } = [];
}
