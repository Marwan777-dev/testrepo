namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>
/// A permission module grant on the wire — used both in
/// <c>PUT /api/v1/users/{userId}/permissions</c> requests and user-detail responses.
/// </summary>
public sealed record ModuleAssignmentDto
{
    public string ModuleId { get; init; } = string.Empty;

    public IReadOnlyList<string> AllowedModes { get; init; } = [];
}
