namespace Nabadat.KpiManagement.Application.Organization;

/// <summary>
/// An opaque, durable handle to a stored logo blob (research.md R3), persisted in
/// <c>organization_settings.logo_blob_ref</c>. <see cref="StorageKey"/> is the storage-relative key
/// (e.g. <c>tenants/{tenantId}/branding/logo.png</c>); only <see cref="ILogoStore"/> interprets it.
/// </summary>
public sealed record LogoBlobRef(string StorageKey);
