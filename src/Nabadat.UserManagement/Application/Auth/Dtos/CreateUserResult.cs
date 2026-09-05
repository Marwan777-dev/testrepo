namespace Nabadat.UserManagement.Application.Auth.Dtos;

/// <summary>Result of <c>TenantAuthenticationService.CreateUserAsync</c>.</summary>
public sealed record CreateUserResult
{
    public required CreateUserOutcome Outcome { get; init; }

    /// <summary>The new user's id when <see cref="Outcome"/> is <see cref="CreateUserOutcome.Created"/>.</summary>
    public Guid? UserId { get; init; }

    public static CreateUserResult Created(Guid userId) =>
        new() { Outcome = CreateUserOutcome.Created, UserId = userId };

    public static CreateUserResult InvalidEmail() => new() { Outcome = CreateUserOutcome.InvalidEmail };

    public static CreateUserResult Conflict() => new() { Outcome = CreateUserOutcome.Conflict };
}
