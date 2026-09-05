namespace Nabadat.UserManagement.Application.Auth.Exceptions;

/// <summary>Thrown when a password-reset token that an admin has revoked is presented.</summary>
public sealed class TokenRevokedException : Exception
{
    public TokenRevokedException(string message = "The reset token has been revoked.") : base(message)
    {
    }
}
