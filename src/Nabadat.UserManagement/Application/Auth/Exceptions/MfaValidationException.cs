namespace Nabadat.UserManagement.Application.Auth.Exceptions;

/// <summary>Thrown when an MFA challenge is rejected (invalid/replayed TOTP code, or an unknown/expired challenge).</summary>
public sealed class MfaValidationException : Exception
{
    public MfaValidationException(string message = "The MFA code is invalid.") : base(message)
    {
    }
}
