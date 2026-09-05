namespace Nabadat.UserManagement.Application.Auth.Exceptions;

/// <summary>Thrown when a password-reset token that has already been redeemed is presented again.</summary>
public sealed class TokenAlreadyUsedException : Exception
{
    public TokenAlreadyUsedException(string message = "The reset token has already been used.") : base(message)
    {
    }
}
