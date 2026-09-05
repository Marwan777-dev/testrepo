using Microsoft.AspNetCore.Http;

namespace Nabadat.CustomerJourneyManagement.Api;

/// <summary>
/// Resolves the per-request correlation id stamped onto M-16's M-17 audit events.
/// </summary>
internal static class ApiCorrelation
{
    /// <summary>
    /// Returns the ASP.NET request trace identifier as a <see cref="Guid"/> when it is
    /// GUID-shaped, otherwise a fresh <see cref="Guid"/>. The default
    /// <see cref="HttpContext.TraceIdentifier"/> format (e.g. <c>0HN…:00000001</c>) is
    /// <b>not</b> a GUID, so a bare <c>Guid.Parse</c> would throw
    /// <see cref="FormatException"/> and surface as a 500 on every write endpoint — this
    /// never throws.
    /// </summary>
    public static Guid CorrelationId(this HttpContext context) =>
        Guid.TryParse(context.TraceIdentifier, out var id) ? id : Guid.NewGuid();
}
