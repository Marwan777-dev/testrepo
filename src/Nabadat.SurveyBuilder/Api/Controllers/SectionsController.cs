using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nabadat.SurveyBuilder.Api.Contracts;
using Nabadat.SurveyBuilder.Application.Exceptions;
using Nabadat.SurveyBuilder.Application.Sections;
using Nabadat.SurveyBuilder.Application.Sections.Dtos;
using Nabadat.SurveyBuilder.Application.Sections.Interfaces;
using Nabadat.UserManagement.Api.Interfaces;

namespace Nabadat.SurveyBuilder.Api.Controllers;

/// <summary>
/// F2 section endpoints (contracts/sections-and-sets.md): add / edit / delete a section. Deletion of
/// a non-empty section requires <c>?confirm=true</c> (FR-2.5) and cascades children (FR-2.6/2.7/2.8).
/// Writes carry an <c>If-Match</c> (Q1); every non-2xx uses the API-05 envelope.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/surveys/{surveyId:guid}/sections")]
public sealed class SectionsController : ControllerBase
{
    private readonly SectionCommandService _commands;
    private readonly SectionCascadeService _cascade;
    private readonly ISectionStore _sections;
    private readonly ISessionContextAccessor _session;

    public SectionsController(
        SectionCommandService commands,
        SectionCascadeService cascade,
        ISectionStore sections,
        ISessionContextAccessor session)
    {
        _commands = commands;
        _cascade = cascade;
        _sections = sections;
        _session = session;
    }

    private Guid ActorId => _session.Current?.UserId ?? throw new SurveyBuilderException("survey.unauthenticated", 401, "No session.");

    [HttpPost]
    public async Task<ActionResult<SectionView>> Create(Guid surveyId, [FromBody] CreateSectionRequest request, CancellationToken ct)
    {
        var section = await _commands.CreateAsync(request.Id, request.ToWriteModel(surveyId), ct);
        SetEtag(section.RowVersion);
        return Created($"/api/v1/surveys/{surveyId}/sections/{section.Id}", SectionView.From(section));
    }

    [HttpPatch("{sectionId:guid}")]
    public async Task<ActionResult<SectionView>> Update(Guid surveyId, Guid sectionId, [FromBody] UpdateSectionRequest request, CancellationToken ct)
    {
        await EnsureEtagMatchesAsync(sectionId, ct);
        var section = await _commands.UpdateAsync(sectionId, request.ToWriteModel(surveyId), ct);
        SetEtag(section.RowVersion);
        return Ok(SectionView.From(section));
    }

    [HttpDelete("{sectionId:guid}")]
    public async Task<IActionResult> Delete(Guid surveyId, Guid sectionId, [FromQuery] bool confirm, CancellationToken ct)
    {
        await EnsureEtagMatchesAsync(sectionId, ct);
        var result = await _cascade.DeleteAsync(new SectionCascadeCommand(sectionId, confirm, ActorId, Guid.NewGuid()), ct);
        if (!result.Deleted)
        {
            throw new SurveyBuilderException(
                result.ErrorCode!, 409, "The section is not empty — resend with confirm=true.", result.Details);
        }

        return Ok();
    }

    private void SetEtag(int rowVersion) => Response.Headers.ETag = $"W/\"{rowVersion}\"";

    private async Task EnsureEtagMatchesAsync(Guid sectionId, CancellationToken ct)
    {
        var ifMatch = Request.Headers.IfMatch.ToString();
        if (string.IsNullOrEmpty(ifMatch))
        {
            throw new SurveyBuilderException("section.etag_required", 400, "If-Match header is required (Q1).");
        }

        var section = await _sections.GetAsync(sectionId, ct)
            ?? throw new SurveyBuilderException("section.not_found", 404, "Section not found.");
        if (ParseWeakEtag(ifMatch) != section.RowVersion)
        {
            throw new SurveyBuilderException("section.conflict", 409, "The section was modified by another writer.");
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
