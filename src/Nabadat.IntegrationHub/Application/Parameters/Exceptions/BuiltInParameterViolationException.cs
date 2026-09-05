namespace Nabadat.IntegrationHub.Application.Parameters.Exceptions;

/// <summary>
/// Thrown by <see cref="BuiltInParameterGuard"/> when a caller attempts an operation that does not exist in the
/// product — deleting any parameter, or renaming/retyping a built-in (BR-09, <c>[PO-G27]</c>).
///
/// <para>It derives from <see cref="InvalidOperationException"/> deliberately: these are not user-correctable
/// field errors (there is no inline message to render and nothing to accumulate), they are attempts at an
/// operation the API does not offer. <see cref="Code"/> carries the stable
/// <see cref="ParameterErrorCodes"/> constant the controller maps to <b>409</b>.</para>
/// </summary>
public sealed class BuiltInParameterViolationException : InvalidOperationException
{
    public BuiltInParameterViolationException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    /// <summary>A <see cref="ParameterErrorCodes"/> constant — what the API layer maps to a status.</summary>
    public string Code { get; }
}
