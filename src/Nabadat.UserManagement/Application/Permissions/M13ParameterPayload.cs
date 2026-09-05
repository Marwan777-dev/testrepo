namespace Nabadat.UserManagement.Application.Permissions;

/// <summary>
/// Inbound payload for <c>POST /api/v1/authorization/scope/parameters</c> — a batch
/// of scope parameter definitions pushed by an external scope provider (M-13).
/// Validated and persisted by <see cref="M13ParameterContractAdapter"/>.
/// </summary>
public sealed record M13ParameterPayload
{
    public required string SourceModule { get; init; }

    public required IReadOnlyList<M13ParameterDefinition> Parameters { get; init; }
}
