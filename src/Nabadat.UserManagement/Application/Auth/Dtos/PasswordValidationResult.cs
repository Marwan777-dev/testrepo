namespace Nabadat.UserManagement.Application.Auth.Dtos;

/// <summary>
/// Outcome of password-complexity validation. <see cref="Errors"/> carries the
/// field-level failure codes (e.g. <c>min_length</c>, <c>missing_uppercase</c>).
/// </summary>
public sealed record PasswordValidationResult
{
    public required bool IsValid { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = [];

    public static PasswordValidationResult Valid() => new() { IsValid = true };

    public static PasswordValidationResult Invalid(IReadOnlyList<string> errors) =>
        new() { IsValid = false, Errors = errors };
}
