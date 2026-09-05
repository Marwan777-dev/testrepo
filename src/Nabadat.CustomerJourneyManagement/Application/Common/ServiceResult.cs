namespace Nabadat.CustomerJourneyManagement.Application.Common;

/// <summary>
/// Outcome of an application-service operation that either succeeds or fails with a typed
/// <see cref="Common.Error"/>. Services return this instead of throwing for expected business
/// failures (name conflicts, invalid transitions, archived-immutable guards), so the API layer
/// can translate <see cref="Common.Error.Code"/> into the API-05 envelope + HTTP status without
/// relying on exceptions for control flow.
/// </summary>
public class ServiceResult
{
    /// <summary>True when the operation succeeded; <see cref="Error"/> is then <c>null</c>.</summary>
    public bool IsSuccess { get; }

    /// <summary>The failure detail when <see cref="IsSuccess"/> is <c>false</c>; otherwise <c>null</c>.</summary>
    public Error? Error { get; }

    /// <summary>Base constructor; use the <see cref="Success()"/>/<see cref="Failure(string,string)"/> factories.</summary>
    protected ServiceResult(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>A successful result with no error.</summary>
    public static ServiceResult Success() => new(isSuccess: true, error: null);

    /// <summary>A failed result carrying the given error <paramref name="code"/> and <paramref name="message"/>.</summary>
    public static ServiceResult Failure(string code, string message) =>
        new(isSuccess: false, error: new Error(code, message));
}

/// <summary>
/// A <see cref="ServiceResult"/> that also carries a payload (<see cref="Value"/>) on success —
/// e.g. the new journey id from a create, or the journey tree from a read.
/// </summary>
/// <typeparam name="T">The success payload type.</typeparam>
public sealed class ServiceResult<T> : ServiceResult
{
    /// <summary>The payload on success; <c>default</c> (e.g. <c>null</c>) when the result is a failure.</summary>
    public T? Value { get; }

    private ServiceResult(bool isSuccess, T? value, Error? error)
        : base(isSuccess, error)
        => Value = value;

    /// <summary>A successful result carrying <paramref name="value"/>.</summary>
    public static ServiceResult<T> Success(T value) => new(isSuccess: true, value: value, error: null);

    /// <summary>A failed result with no payload, carrying the given error <paramref name="code"/> and <paramref name="message"/>.</summary>
    public static new ServiceResult<T> Failure(string code, string message) =>
        new(isSuccess: false, value: default, error: new Error(code, message));
}
