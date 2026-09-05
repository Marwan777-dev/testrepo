namespace Nabadat.SurveyBuilder.Application.Interfaces;

/// <summary>
/// Request-scoped carrier for optimistic-concurrency ETags (research.md §2 — <c>row_version</c> is
/// the monotonic ETag counter). <c>EtagMiddleware</c> (T023) parses the inbound
/// <c>If-Match: W/"&lt;n&gt;"</c> into <see cref="IfMatch"/> and, on the way out, stamps
/// <c>ETag: W/"&lt;n&gt;"</c> from <see cref="ResponseRowVersion"/>. A write handler reads
/// <see cref="IfMatch"/>, compares it to the aggregate's current <c>row_version</c>, and on a
/// mismatch throws an <c>&lt;aggregate&gt;.conflict</c> error (412); on success it sets
/// <see cref="ResponseRowVersion"/> to the persisted <c>row_version</c> so the fresh ETag ships.
/// </summary>
/// <remarks>
/// An Application-owned abstraction (peer of <c>ICurrentTenant</c>) so services can read/set it
/// without referencing the Api layer; the scoped implementation lives in <c>Api/Accessors</c>.
/// </remarks>
public interface ICurrentETag
{
    /// <summary>The weak-ETag revision parsed from the request's <c>If-Match</c> header; null when absent.</summary>
    int? IfMatch { get; set; }

    /// <summary>The aggregate <c>row_version</c> to stamp as the response <c>ETag</c>; null ⇒ no ETag emitted.</summary>
    int? ResponseRowVersion { get; set; }
}
