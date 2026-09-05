using Nabadat.KpiManagement.Application.Organization.Dtos;
using Nabadat.KpiManagement.Application.Organization.Interfaces;
using Nabadat.UserManagement.Application.Interfaces;

namespace Nabadat.KpiManagement.Application.Organization;

/// <summary>
/// Orchestrates the Organization editing surface (US-6, FR-050) — all M-06-internal, no cross-module
/// hop. <see cref="SaveSettingsAsync"/> validates Name/Industry then delegates to the atomic
/// <see cref="IOrganizationSettingsStore.UpdateAsync"/>. <see cref="SaveLogoAsync"/> validates the
/// upload, sanitises SVG bytes (persisting the SANITISED stream, never the upload), stores the blob
/// via <see cref="ILogoStore"/>, then records the new ref atomically via
/// <see cref="IOrganizationSettingsStore.UpdateLogoRefAsync"/>. Time and tenant are injected.
/// </summary>
public sealed class OrganizationSaveService
{
    public const string SvgUnsafeContentCode = "logo.svg.unsafe_content";

    private readonly IOrganizationSettingsStore _store;
    private readonly ILogoStore _logoStore;
    private readonly OrganizationSettingsValidator _validator;
    private readonly LogoUploadValidator _logoValidator;
    private readonly SvgSanitiser _sanitiser;
    private readonly ICurrentTenant _tenant;
    private readonly TimeProvider _time;

    public OrganizationSaveService(
        IOrganizationSettingsStore store,
        ILogoStore logoStore,
        OrganizationSettingsValidator validator,
        LogoUploadValidator logoValidator,
        SvgSanitiser sanitiser,
        ICurrentTenant tenant,
        TimeProvider time)
    {
        _store = store;
        _logoStore = logoStore;
        _validator = validator;
        _logoValidator = logoValidator;
        _sanitiser = sanitiser;
        _tenant = tenant;
        _time = time;
    }

    public async Task<OrganizationSaveResult> SaveSettingsAsync(
        OrganizationSettingsUpdate update,
        Guid actorId,
        string actorPersona,
        Guid correlationId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        var validation = await _validator.ValidateAsync(update, ct);
        if (!validation.IsValid)
        {
            return new OrganizationSaveResult(false, validation.Errors[0].ErrorCode, null);
        }

        var settings = await _store.UpdateAsync(
            update.Name!, update.Industry!, actorId, actorPersona, correlationId, _time.GetUtcNow(), ct);

        return new OrganizationSaveResult(true, null, settings);
    }

    public async Task<LogoSaveResult> SaveLogoAsync(
        string contentType,
        byte[] payload,
        Guid actorId,
        string actorPersona,
        Guid correlationId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var validation = _logoValidator.Validate(contentType, payload.LongLength);
        if (validation.Outcome == LogoValidationOutcome.Invalid)
        {
            return Failed(validation.Code);
        }

        // For SVG, persist the SANITISED bytes (FR-050) — never the upload bytes.
        var bytes = payload;
        var wasSanitised = false;
        if (string.Equals(contentType, "image/svg+xml", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var sanitised = _sanitiser.SanitiseDetailed(payload);
                bytes = sanitised.Bytes;
                wasSanitised = sanitised.WasModified;
            }
            catch (SvgUnsafeContentException)
            {
                return Failed(SvgUnsafeContentCode);
            }
        }

        using var stream = new MemoryStream(bytes, writable: false);
        var blobRef = await _logoStore.PutAsync(_tenant.TenantId, contentType, stream, ct);

        await _store.UpdateLogoRefAsync(blobRef.StorageKey, actorId, actorPersona, correlationId, _time.GetUtcNow(), ct);

        return new LogoSaveResult(true, null, blobRef, contentType, bytes.LongLength, wasSanitised);
    }

    private static LogoSaveResult Failed(string? code) =>
        new(false, code, null, null, 0, false);
}
