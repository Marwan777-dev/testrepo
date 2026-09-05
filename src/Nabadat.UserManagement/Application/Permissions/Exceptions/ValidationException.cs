namespace Nabadat.UserManagement.Application.Permissions.Exceptions;

/// <summary>
/// Thrown when a scope assignment or parameter-definition payload fails validation
/// before any write. Carries one or more <see cref="ValidationFailure"/> entries so
/// the API layer can surface them as the API-05 envelope's <c>details</c> array
/// (400/422 responses, permissions-api.md).
/// </summary>
public sealed class ValidationException : Exception
{
    public IReadOnlyList<ValidationFailure> Failures { get; }

    public ValidationException(IReadOnlyList<ValidationFailure> failures, string? message = null)
        : base(message ?? "One or more validation failures occurred.")
        => Failures = failures;

    public ValidationException(string field, string code, string? message = null)
        : this([new ValidationFailure(field, code)], message)
    {
    }
}
