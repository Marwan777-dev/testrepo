using Nabadat.SurveyBuilder.Application.Interfaces;

namespace Nabadat.SurveyBuilder.Api.Accessors;

/// <summary>
/// Request-scoped implementation of <see cref="ICurrentETag"/>. Registered <c>Scoped</c> so the
/// value set by <c>EtagMiddleware</c> on ingress and by the write handler mid-request is the same
/// instance the middleware reads on egress. Mirrors the M-10 <c>RequestCurrentTenant</c> accessor
/// placement in <c>Api/Accessors</c>.
/// </summary>
public sealed class CurrentETag : ICurrentETag
{
    public int? IfMatch { get; set; }

    public int? ResponseRowVersion { get; set; }
}
