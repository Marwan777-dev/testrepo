using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nabadat.SurveyBuilder.Api.Contracts;
using Nabadat.SurveyBuilder.Application.Exceptions;
using Nabadat.SurveyBuilder.Application.Templates;
using Nabadat.SurveyBuilder.Application.Templates.Dtos;
using Nabadat.SurveyBuilder.Domain.ValueObjects;
using Nabadat.UserManagement.Api.Interfaces;

namespace Nabadat.SurveyBuilder.Api.Controllers;

/// <summary>
/// F6 / F7 template endpoints (contracts/templates.md): list/search, get (redacted summary),
/// preview, save-as-template, metadata edit, rebuild-from-survey, delete, and instantiate.
/// Authentication is enforced by <c>[Authorize]</c>; the actor is read from
/// <see cref="ISessionContextAccessor"/>. Customized templates carry an <c>ETag</c> and honour
/// <c>If-Match</c> on writes; built-in templates are read-only (writes 403 via the command service).
/// Every non-2xx uses the API-05 envelope (via <c>ApiErrorEnvelopeMiddleware</c>).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/templates")]
public sealed class TemplatesController : ControllerBase
{
    private readonly TemplateCommandService _commands;
    private readonly TemplateSearchService _search;
    private readonly ISessionContextAccessor _session;

    public TemplatesController(
        TemplateCommandService commands,
        TemplateSearchService search,
        ISessionContextAccessor session)
    {
        _commands = commands;
        _search = search;
        _session = session;
    }

    private Guid ActorId => _session.Current?.UserId
        ?? throw new SurveyBuilderException("survey.unauthenticated", 401, "No session.");

    [HttpGet]
    public async Task<ActionResult<TemplateListResponse>> List(
        [FromQuery] string? q,
        [FromQuery] string? search,
        [FromQuery(Name = "class")] string? templateClass,
        [FromQuery] string? sector,
        [FromQuery] string sort = "updated_at",
        [FromQuery] string order = "desc",
        [FromQuery(Name = "page_size")] int pageSize = 50,
        [FromQuery(Name = "page_token")] string? pageToken = null,
        CancellationToken ct = default)
    {
        // The contract's search term is `q`; `search` is accepted as an alias (spec/tasks wording).
        var term = string.IsNullOrWhiteSpace(q) ? search : q;
        TemplateClass? cls = Enum.TryParse<TemplateClass>(templateClass, ignoreCase: true, out var parsed) ? parsed : null;
        var result = await _search.SearchAsync(new TemplateSearchQuery(term, cls, sector, sort, order, pageSize, pageToken), ct);
        var items = result.Items.Select(TemplateListItem.From).ToList();
        return Ok(new TemplateListResponse(items, result.NextPageToken, result.TotalCount));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TemplateView>> Get(Guid id, CancellationToken ct)
    {
        var template = await _commands.GetAsync(id, ct)
            ?? throw new SurveyBuilderException("template.not_found", 404, "Template not found.");
        var snapshot = await _commands.GetSnapshotAsync(id, ct);
        if (template.Class == TemplateClass.Customized)
        {
            SetEtag(template.RowVersion); // BuiltIn templates are read-only — no ETag (contracts/templates.md)
        }

        return Ok(TemplateView.From(template, snapshot));
    }

    [HttpGet("{id:guid}/preview")]
    public async Task<ActionResult<SurveyView>> Preview(Guid id, CancellationToken ct)
    {
        var survey = await _commands.BuildPreviewSurveyAsync(id, ActorId, ct);
        return Ok(SurveyView.From(survey));
    }

    [HttpPost]
    public async Task<ActionResult<TemplateView>> Create([FromBody] CreateTemplateRequest request, CancellationToken ct)
    {
        var template = await _commands.CreateFromSurveyAsync(
            request.SourceSurveyId, request.NameEn, request.NameAr, request.Description,
            request.Tags ?? Array.Empty<string>(), ActorId, ct);
        var snapshot = await _commands.GetSnapshotAsync(template.Id, ct);
        SetEtag(template.RowVersion);
        return CreatedAtAction(nameof(Get), new { id = template.Id }, TemplateView.From(template, snapshot));
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<TemplateView>> Update(Guid id, [FromBody] UpdateTemplateRequest request, CancellationToken ct)
    {
        await EnsureEtagMatchesAsync(id, ct);
        var template = await _commands.UpdateAsync(id, request.ToPatch(), ActorId, ct);
        var snapshot = await _commands.GetSnapshotAsync(id, ct);
        SetEtag(template.RowVersion);
        return Ok(TemplateView.From(template, snapshot));
    }

    [HttpPost("{id:guid}/rebuild-from-survey")]
    public async Task<ActionResult<TemplateView>> Rebuild(Guid id, [FromBody] RebuildTemplateRequest request, CancellationToken ct)
    {
        var template = await _commands.RebuildFromSurveyAsync(id, request.SourceSurveyId, ActorId, ct);
        var snapshot = await _commands.GetSnapshotAsync(id, ct);
        SetEtag(template.RowVersion);
        return Ok(TemplateView.From(template, snapshot));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await EnsureEtagMatchesAsync(id, ct);
        await _commands.DeleteAsync(id, ct);
        return Ok();
    }

    [HttpPost("{id:guid}/instantiate")]
    public async Task<ActionResult<SurveyView>> Instantiate(Guid id, [FromBody] InstantiateTemplateRequest? request, CancellationToken ct)
    {
        var survey = await _commands.InstantiateAsync(id, request?.NameEn, ActorId, ct);
        SetEtag(survey.RowVersion);
        return CreatedAtAction("Get", "Surveys", new { id = survey.Id }, SurveyView.From(survey));
    }

    private void SetEtag(int rowVersion) => Response.Headers.ETag = $"W/\"{rowVersion}\"";

    private async Task EnsureEtagMatchesAsync(Guid id, CancellationToken ct)
    {
        var template = await _commands.GetAsync(id, ct)
            ?? throw new SurveyBuilderException("template.not_found", 404, "Template not found.");

        // Built-in templates are read-only: skip the ETag gate so the command service returns the
        // authoritative 403 (template.built_in_not_editable) rather than a 400/409 about the ETag.
        if (template.Class == TemplateClass.BuiltIn)
        {
            return;
        }

        var ifMatch = Request.Headers.IfMatch.ToString();
        if (string.IsNullOrEmpty(ifMatch))
        {
            throw new SurveyBuilderException("template.etag_required", 400, "If-Match header is required.");
        }

        var expected = ParseWeakEtag(ifMatch);
        if (expected is null || expected != template.RowVersion)
        {
            throw new SurveyBuilderException("template.conflict", 409, "The template was modified by another writer.");
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
