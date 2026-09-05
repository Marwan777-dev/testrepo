using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nabadat.SurveyBuilder.Api.Contracts;
using Nabadat.SurveyBuilder.Api.Filters;
using Nabadat.SurveyBuilder.Application.Exceptions;
using Nabadat.SurveyBuilder.Application.Surveys;
using Nabadat.SurveyBuilder.Application.Surveys.Dtos;
using Nabadat.SurveyBuilder.Application.Surveys.Interfaces;
using Nabadat.UserManagement.Api.Interfaces;

namespace Nabadat.SurveyBuilder.Api.Controllers;

/// <summary>
/// F15 approval-workflow endpoints (US2, contracts/approval-workflow.md): submit a Draft for review,
/// publish (reviewer or grant-holding author), and non-destructive return-to-draft. Delegates to
/// <see cref="ApprovalWorkflowService"/>. Every write requires <c>If-Match</c> and returns the new
/// <c>ETag</c>; <c>publish</c> additionally requires <c>Idempotency-Key</c> (enforced by the module
/// middleware). Non-2xx uses the API-05 envelope. The <see cref="EditLockFilter"/> on return-to-draft
/// keeps this endpoint to the PendingReview → Draft path — an Active/Paused survey is redirected to the
/// destructive <c>POST /status</c> flow (BR-1.6).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/surveys")]
public sealed class SurveyLifecycleController : ControllerBase
{
    private readonly ApprovalWorkflowService _approval;
    private readonly ISurveyStore _surveys;
    private readonly ISessionContextAccessor _session;

    public SurveyLifecycleController(
        ApprovalWorkflowService approval,
        ISurveyStore surveys,
        ISessionContextAccessor session)
    {
        _approval = approval;
        _surveys = surveys;
        _session = session;
    }

    private Guid ActorId => _session.Current?.UserId ?? throw new SurveyBuilderException("survey.unauthenticated", 401, "No session.");

    private string ActorRole => _session.Current?.Persona ?? "P-00";

    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<ApprovalActionResult>> Submit(Guid id, [FromBody] SubmitSurveyRequest? request, CancellationToken ct)
    {
        await EnsureEtagMatchesAsync(id, ct);
        var result = await _approval.SubmitAsync(
            new SubmitForReviewCommand(id, ActorId, ActorRole, Guid.NewGuid()), ct);
        return Respond(id, result);
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<ActionResult<ApprovalActionResult>> Publish(Guid id, [FromBody] PublishSurveyRequest? request, CancellationToken ct)
    {
        await EnsureEtagMatchesAsync(id, ct);
        var result = await _approval.PublishAsync(
            new PublishSurveyCommand(id, ActorId, ActorRole, request?.Remarks, Guid.NewGuid()), ct);
        return Respond(id, result);
    }

    [HttpPost("{id:guid}/return-to-draft")]
    [ServiceFilter(typeof(EditLockFilter))]
    public async Task<ActionResult<ApprovalActionResult>> ReturnToDraft(Guid id, [FromBody] ReturnToDraftRequest request, CancellationToken ct)
    {
        await EnsureEtagMatchesAsync(id, ct);
        var result = await _approval.ReturnToDraftAsync(
            new ReturnForRevisionCommand(id, ActorId, ActorRole, request.Remarks, Guid.NewGuid()), ct);
        return Respond(id, result);
    }

    private ActionResult<ApprovalActionResult> Respond(Guid id, SurveyTransitionResult result)
    {
        SetEtag(result.RowVersion);
        return Ok(new ApprovalActionResult(id, result.Status.ToString(), result.RowVersion));
    }

    private void SetEtag(int rowVersion) => Response.Headers.ETag = $"W/\"{rowVersion}\"";

    private async Task EnsureEtagMatchesAsync(Guid id, CancellationToken ct)
    {
        var ifMatch = Request.Headers.IfMatch.ToString();
        if (string.IsNullOrEmpty(ifMatch))
        {
            throw new SurveyBuilderException("survey.etag_required", 400, "If-Match header is required (Q1).");
        }

        var expected = ParseWeakEtag(ifMatch);
        var survey = await _surveys.GetAsync(id, ct)
            ?? throw new SurveyBuilderException("survey.not_found", 404, "Survey not found.");
        if (expected is null || expected != survey.RowVersion)
        {
            throw new SurveyBuilderException("survey.conflict", 409, "The survey was modified by another writer.");
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
