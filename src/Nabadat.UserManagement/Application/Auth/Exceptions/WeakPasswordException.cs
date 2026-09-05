namespace Nabadat.UserManagement.Application.Auth.Exceptions;

/// <summary>Thrown when a new password fails complexity validation (maps to API 422 weak_password).</summary>
public sealed class WeakPasswordException : Exception
{
    public WeakPasswordException(IReadOnlyList<string> errors)
        : base("Password does not meet complexity requirements.")
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
