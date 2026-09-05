namespace Nabadat.UserManagement.Application.Auth.Dtos;

/// <summary>Result discriminator for <see cref="CreateUserResult"/>.</summary>
public enum CreateUserOutcome
{
    /// <summary>User was created.</summary>
    Created,

    /// <summary>The supplied username was not a valid email address.</summary>
    InvalidEmail,

    /// <summary>A user with the same username already exists in the tenant.</summary>
    Conflict,
}
