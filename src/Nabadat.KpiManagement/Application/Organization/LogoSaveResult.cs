namespace Nabadat.KpiManagement.Application.Organization;

/// <summary>
/// The outcome of <see cref="OrganizationSaveService.SaveLogoAsync"/>: on success the persisted
/// <see cref="BlobRef"/> plus the stored <see cref="ContentType"/> / <see cref="SizeBytes"/> and the
/// <see cref="WasSanitised"/> flag (true only when an SVG had content stripped — surfaced as
/// <c>was_sanitised</c>). On failure, the dotted application <see cref="ErrorCode"/>.
/// </summary>
public sealed record LogoSaveResult(
    bool Succeeded,
    string? ErrorCode,
    LogoBlobRef? BlobRef,
    string? ContentType,
    long SizeBytes,
    bool WasSanitised);
