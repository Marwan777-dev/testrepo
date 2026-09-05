using Nabadat.UserManagement.Application.Auth.Dtos;
using Nabadat.UserManagement.Application.Auth.Interfaces;

namespace Nabadat.UserManagement.Application.Auth;

/// <summary>
/// Enforces password complexity: minimum 10 characters with at least one uppercase,
/// one lowercase, one digit, and one special (non-alphanumeric) character. Returns
/// every failing rule's code so the caller can surface field-level guidance.
/// </summary>
public sealed class PasswordValidator : IPasswordValidator
{
    private const int MinLength = 10;

    public PasswordValidationResult ValidatePassword(string password)
    {
        password ??= string.Empty;
        var errors = new List<string>();

        if (password.Length < MinLength)
        {
            errors.Add("min_length");
        }

        if (!password.Any(char.IsUpper))
        {
            errors.Add("missing_uppercase");
        }

        if (!password.Any(char.IsLower))
        {
            errors.Add("missing_lowercase");
        }

        if (!password.Any(char.IsDigit))
        {
            errors.Add("missing_digit");
        }

        if (!password.Any(c => !char.IsLetterOrDigit(c)))
        {
            errors.Add("missing_special");
        }

        return errors.Count == 0
            ? PasswordValidationResult.Valid()
            : PasswordValidationResult.Invalid(errors);
    }
}
