using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nabadat.SurveyBuilder.Api.Contracts;
using Nabadat.SurveyBuilder.Application.Exceptions;
using Nabadat.SurveyBuilder.Application.Translations;
using Nabadat.SurveyBuilder.Application.Translations.Interfaces;

namespace Nabadat.SurveyBuilder.Api.Controllers;

/// <summary>
/// F11 Translate workspace endpoints (contracts/translations.md): list per-locale coverage, read a
/// resolved locale bundle (target + English fallback + missing keys), and save a target bundle with
/// merge semantics. Writes carry an <c>If-Match</c> ETag on updates; a first-time locale create may
/// omit it (or send <c>*</c>). Authentication via <c>[Authorize]</c>; errors via the API-05 envelope.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/surveys/{id:guid}/translations")]
public sealed class SurveyTranslationsController : ControllerBase
{
    private readonly TranslationBundleService _translationsService;
    private readonly ITranslationStore _translations;

    public SurveyTranslationsController(TranslationBundleService translationsService, ITranslationStore translations)
    {
        _translationsService = translationsService;
        _translations = translations;
    }

    [HttpGet]
    public async Task<ActionResult<TranslationLocalesResponse>> GetLocales(Guid id, CancellationToken ct)
    {
        var coverage = await _translationsService.GetLocalesAsync(id, ct);
        return Ok(new TranslationLocalesResponse(coverage.Select(LocaleSummary.From).ToList()));
    }

    [HttpGet("{locale}")]
    public async Task<ActionResult<TranslationBundleView>> GetBundle(Guid id, string locale, CancellationToken ct)
    {
        var result = await _translationsService.GetBundleAsync(id, locale, ct);
        SetEtag(result.RowVersion);
        return Ok(TranslationBundleView.From(result.Bundle));
    }

    [HttpPut("{locale}")]
    public async Task<ActionResult<TranslationBundleView>> PutBundle(
        Guid id,
        string locale,
        [FromBody] PutTranslationBundleRequest request,
        CancellationToken ct)
    {
        await EnsureEtagPreconditionAsync(id, locale, ct);
        var result = await _translationsService.PutBundleAsync(id, locale, request.ToBundle(), ct);
        SetEtag(result.RowVersion);
        return Ok(TranslationBundleView.From(result.Bundle));
    }

    private void SetEtag(int rowVersion)
    {
        if (rowVersion > 0)
        {
            Response.Headers.ETag = $"W/\"{rowVersion}\"";
        }
    }

    /// <summary>
    /// On an <b>update</b> (a row already exists) an <c>If-Match</c> is required and must match the
    /// stored <c>row_version</c> — a mismatch is a 409. A first-time locale create (no row yet) may
    /// omit <c>If-Match</c>, and <c>If-Match: *</c> is an unconditional overwrite.
    /// </summary>
    private async Task EnsureEtagPreconditionAsync(Guid surveyId, string locale, CancellationToken ct)
    {
        var existing = await _translations.GetAsync(surveyId, locale, ct);
        if (existing is null)
        {
            return; // first write — no precondition
        }

        var ifMatch = Request.Headers.IfMatch.ToString();
        if (ifMatch.Trim() == "*")
        {
            return; // unconditional overwrite
        }

        if (string.IsNullOrEmpty(ifMatch))
        {
            throw new SurveyBuilderException("translation.etag_required", 400, "If-Match header is required on updates.");
        }

        if (ParseWeakEtag(ifMatch) != existing.RowVersion)
        {
            throw new SurveyBuilderException("translation.conflict", 409, "The translation bundle was modified by another writer.");
        }
    }

    private static int? ParseWeakEtag(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[2..];
        }

        trimmed = trimmed.Trim('"');
        return int.TryParse(trimmed, out var version) ? version : null;
    }
}
