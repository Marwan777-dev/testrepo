namespace Nabadat.UserManagement.Application.Auth.Exceptions;

/// <summary>Thrown when a password-reset token is redeemed after its expiry.</summary>
public sealed class TokenExpiredException : Exception
{
    public TokenExpiredException(string message = "The reset token has expired.") : base(message)
    {
    }
}
