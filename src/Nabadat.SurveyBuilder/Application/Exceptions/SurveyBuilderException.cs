namespace Nabadat.SurveyBuilder.Application.Exceptions;

/// <summary>
/// Base type for every M-01 domain/application error that maps to an API-05 response. Carries the
/// dot-namespaced error <see cref="Code"/> (research.md §9), the HTTP <see cref="StatusCode"/> to
/// return, and optional structured <see cref="Details"/>. <c>ApiErrorEnvelopeMiddleware</c> (T025)
/// catches these and renders the envelope; any non-M-01 exception it treats as a 500.
/// <para>Per-surface exceptions (e.g. a survey-name validation error, a status-transition error)
/// derive from this in <c>Application/&lt;SubDomain&gt;/Exceptions/</c> as the US phases ship them,
/// each supplying its own code + status.</para>
/// </summary>
public class SurveyBuilderException : Exception
{
    public SurveyBuilderException(
        string code,
        int statusCode,
        string message,
        IReadOnlyDictionary<string, object>? details = null)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
        Details = details;
    }

    /// <summary>Dot-namespaced error code (research.md §9), e.g. <c>survey.conflict</c>.</summary>
    public string Code { get; }

    /// <summary>HTTP status the envelope is written with.</summary>
    public int StatusCode { get; }

    /// <summary>Optional structured context surfaced under the envelope's <c>details</c>.</summary>
    public IReadOnlyDictionary<string, object>? Details { get; }
}
