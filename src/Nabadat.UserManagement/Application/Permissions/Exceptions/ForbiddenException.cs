namespace Nabadat.UserManagement.Application.Permissions.Exceptions;

/// <summary>
/// Thrown when an actor is not authorised to perform a data-layer operation — e.g. a
/// non-P-01/P-07 persona attempting user creation, or P-07 attempting to assign a
/// CX-domain permission module. Authorization is enforced here at the service/data
/// layer (not only in the controller), so it holds even when the API is bypassed.
/// The API boundary maps this to HTTP 403 with the API-05 error envelope, using
/// <see cref="Code"/> as the machine-readable error code.
/// </summary>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message, string code = "authorization.forbidden")
        : base(message) => Code = code;

    /// <summary>Machine-readable API-05 error code (e.g. <c>authorization.forbidden</c>).</summary>
    public string Code { get; }
}
