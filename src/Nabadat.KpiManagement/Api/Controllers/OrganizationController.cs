using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nabadat.KpiManagement.Api.Contracts;
using Nabadat.KpiManagement.Application.Organization;
using Nabadat.KpiManagement.Application.Organization.Dtos;
using Nabadat.KpiManagement.Application.Organization.Interfaces;
using Nabadat.UserManagement.Api.Authorization;
using Nabadat.UserManagement.Api.Contracts;
using Nabadat.UserManagement.Api.Interfaces;

namespace Nabadat.KpiManagement.Api.Controllers;

/// <summary>
/// Tenant Organization settings endpoints (contracts/settings-api.md, US-6):
/// <list type="bullet">
///   <item><c>GET /api/v1/tenant/organization</c> — current Name / Logo / Industry + the canonical
///   <c>industry_options</c>.</item>
///   <item><c>PUT /api/v1/tenant/organization</c> — update Name + Industry.</item>
///   <item><c>POST /api/v1/tenant/organization/logo</c> — upload (multipart); SVG is sanitised on
///   upload and the response reports <c>was_sanitised</c>.</item>
///   <item><c>GET /api/v1/tenant/organization/logo</c> — serves the persisted (sanitised) bytes; the
///   <c>logo.url</c> in the other responses points here.</item>
/// </list>
/// All M-06-internal (the table + surface are M-06-owned, re-homed from the never-built M-11). The
/// surface is gated on the <c>TenantConfiguration</c> module (P-01 / P-07 per FR-052): reads need
/// <c>View</c>, writes need <c>Manage</c>. Every non-2xx uses the API-05 envelope.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/tenant/organization")]
public sealed class OrganizationController : ControllerBase
{
    /// <summary>App-relative URL that serves the persisted logo bytes (the <c>GET …/logo</c> route).</summary>
    private const string LogoUrl = "/api/v1/tenant/organization/logo";

    /// <summary>Hard cap (10 MB) guarding against denial-of-storage; the 2 MB soft limit only warns.</summary>
    private const long HardMaxLogoBytes = 10L * 1024 * 1024;

    private readonly IOrganizationSettingsStore _store;
    private readonly IIndustryEnumProvider _industries;
    private readonly ILogoStore _logoStore;
    private readonly OrganizationSaveService _saveService;
    private readonly ISessionContextAccessor _session;

    public OrganizationController(
        IOrganizationSettingsStore store,
        IIndustryEnumProvider industries,
        ILogoStore logoStore,
        OrganizationSaveService saveService,
        ISessionContextAccessor session)
    {
        _store = store;
        _industries = industries;
        _logoStore = logoStore;
        _saveService = saveService;
        _session = session;
    }

    /// <summary>GET /api/v1/tenant/organization — current settings + canonical industry options.</summary>
    [HttpGet]
    [RequirePermission(PermissionModule.TenantConfiguration, PermissionMode.View)]
    [ProducesResponseType(typeof(OrganizationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOrganization(CancellationToken ct = default) =>
        Ok(await BuildResponseAsync(ct));

    /// <summary>PUT /api/v1/tenant/organization — update Name + Industry.</summary>
    [HttpPut]
    [RequirePermission(PermissionModule.TenantConfiguration, PermissionMode.Manage)]
    [ProducesResponseType(typeof(OrganizationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateOrganization(
        [FromBody] OrganizationUpdateRequest request,
        CancellationToken ct = default)
    {
        var actor = CurrentActor();
        var result = await _saveService.SaveSettingsAsync(
            new OrganizationSettingsUpdate(request.Name, request.Industry),
            actor.UserId, actor.Persona, Correlation(), ct);

        return result.Succeeded
            ? Ok(await BuildResponseAsync(ct))
            : MapOrganizationError(result.ErrorCode);
    }

    /// <summary>POST /api/v1/tenant/organization/logo — upload (or replace) the logo (multipart/form-data).</summary>
    [HttpPost("logo")]
    [RequirePermission(PermissionModule.TenantConfiguration, PermissionMode.Manage)]
    [ProducesResponseType(typeof(LogoUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> UploadLogo(IFormFile? logo, CancellationToken ct = default)
    {
        if (logo is null || logo.Length <= 0)
        {
            return BadRequest(Envelope("LOGO_SIZE_ZERO", "The uploaded logo is empty."));
        }

        if (logo.Length > HardMaxLogoBytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge,
                Envelope("LOGO_TOO_LARGE", "The logo exceeds the 10 MB maximum."));
        }

        byte[] bytes;
        await using (var buffer = new MemoryStream())
        {
            await logo.CopyToAsync(buffer, ct);
            bytes = buffer.ToArray();
        }

        var actor = CurrentActor();
        var result = await _saveService.SaveLogoAsync(
            logo.ContentType, bytes, actor.UserId, actor.Persona, Correlation(), ct);

        if (!result.Succeeded)
        {
            return MapLogoError(result.ErrorCode);
        }

        return Ok(new LogoUploadResponse
        {
            Url = LogoUrl,
            ContentType = result.ContentType!,
            SizeBytes = result.SizeBytes,
            WasSanitised = result.WasSanitised,
        });
    }

    /// <summary>
    /// GET /api/v1/tenant/organization/logo — streams the persisted (sanitised) logo bytes. Anonymous
    /// by design: the logo is tenant-public branding (the tenant is resolved from the subdomain, not
    /// the token), so the <c>logo.url</c> can load directly in an <c>&lt;img src&gt;</c> without a
    /// bearer header. No user input reaches the storage path — the blob ref is read from the DB.
    /// </summary>
    [HttpGet("logo")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLogo(CancellationToken ct = default)
    {
        var settings = await _store.GetAsync(ct);
        if (settings?.LogoBlobRef is null)
        {
            return NotFound(Envelope("LOGO_NOT_FOUND", "No logo has been uploaded."));
        }

        var stream = await _logoStore.GetAsync(new LogoBlobRef(settings.LogoBlobRef), ct);
        return File(stream, LogoStore.ContentTypeFor(settings.LogoBlobRef));
    }

    // ----- helpers ---------------------------------------------------------------------------

    private async Task<OrganizationResponse> BuildResponseAsync(CancellationToken ct)
    {
        var settings = await _store.GetAsync(ct);
        var options = _industries.GetAll().Select(i => i.ToString()).ToList();

        OrganizationLogoDto? logo = null;
        if (settings?.LogoBlobRef is not null)
        {
            long size = 0;
            await using (var stream = await _logoStore.GetAsync(new LogoBlobRef(settings.LogoBlobRef), ct))
            {
                size = stream.CanSeek ? stream.Length : 0;
            }

            logo = new OrganizationLogoDto
            {
                Url = LogoUrl,
                ContentType = LogoStore.ContentTypeFor(settings.LogoBlobRef),
                SizeBytes = size,
            };
        }

        return new OrganizationResponse
        {
            Name = settings?.Name ?? string.Empty,
            Logo = logo,
            Industry = settings?.Industry ?? string.Empty,
            IndustryOptions = options,
            Audit = settings is null ? null : new OrganizationAuditDto
            {
                UpdatedAt = settings.UpdatedAt,
                UpdatedBy = settings.UpdatedBy,
            },
        };
    }

    private IActionResult MapOrganizationError(string? code) => code switch
    {
        OrganizationSettingsValidator.NameRequiredCode =>
            BadRequest(Envelope("ORGANIZATION_NAME_REQUIRED", "Organization name is required.")),
        OrganizationSettingsValidator.NameTooLongCode =>
            BadRequest(Envelope("ORGANIZATION_NAME_TOO_LONG", "Organization name is too long (max 150 characters).")),
        OrganizationSettingsValidator.IndustryUnknownCode =>
            BadRequest(Envelope("ORGANIZATION_INDUSTRY_UNKNOWN", "Industry is not one of the supported values.")),
        _ => BadRequest(Envelope("ORGANIZATION_VALIDATION_FAILED", "The organization settings are invalid.")),
    };

    private IActionResult MapLogoError(string? code) => code switch
    {
        LogoUploadValidator.ContentTypeUnsupportedCode =>
            BadRequest(Envelope("LOGO_CONTENT_TYPE_UNSUPPORTED", "Logo must be a PNG, JPG, or SVG file.")),
        LogoUploadValidator.SizeZeroCode =>
            BadRequest(Envelope("LOGO_SIZE_ZERO", "The uploaded logo is empty.")),
        OrganizationSaveService.SvgUnsafeContentCode =>
            BadRequest(Envelope("LOGO_SVG_UNSAFE_CONTENT", "The SVG could not be made safe and was rejected.")),
        _ => BadRequest(Envelope("LOGO_UPLOAD_FAILED", "The logo could not be uploaded.")),
    };

    private (Guid UserId, string Persona) CurrentActor()
    {
        var session = _session.Current;
        return session is null ? (Guid.Empty, "P-01") : (session.UserId, session.Persona);
    }

    private Guid Correlation() =>
        Guid.TryParse(HttpContext.TraceIdentifier, out var g) ? g : Guid.NewGuid();

    private ApiErrorEnvelope Envelope(string code, string message) => new()
    {
        Error = new ApiErrorDetail
        {
            Code = code,
            Message = message,
            CorrelationId = Correlation().ToString(),
        },
    };
}
