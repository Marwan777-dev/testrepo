namespace Nabadat.KpiManagement.Application.Organization.Interfaces;

/// <summary>
/// Storage abstraction for tenant logo blobs (research.md R3). M-06-internal (re-homed from the
/// never-built M-11, 2026-06-24). The implementation routes to the tenant's configured storage
/// region (T-04) — S3-compatible object store in SaaS, a filesystem mount on-prem (AD-05) — behind
/// an identical interface. Bytes live in blob storage, never the tenant DB.
/// </summary>
public interface ILogoStore
{
    /// <summary>Stores (or replaces) the tenant's logo and returns its durable <see cref="LogoBlobRef"/>.
    /// For SVG, the caller passes the SANITISED bytes (FR-050) — this store persists exactly what it
    /// is given.</summary>
    Task<LogoBlobRef> PutAsync(Guid tenantId, string contentType, Stream payload, CancellationToken ct = default);

    /// <summary>Opens a read stream over a previously-stored blob.</summary>
    Task<Stream> GetAsync(LogoBlobRef blobRef, CancellationToken ct = default);
}
