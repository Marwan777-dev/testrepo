using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nabadat.UserManagement.Api.Contracts;
using Nabadat.UserManagement.Api.Interfaces;
using Nabadat.UserManagement.Application.Interfaces;
using Nabadat.UserManagement.Application.Permissions;
using Nabadat.UserManagement.Application.Permissions.Exceptions;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.ValueObjects;
using Nabadat.UserManagement.Application.Permissions.Interfaces;

namespace Nabadat.UserManagement.Api.Controllers;

/// <summary>
/// Persona authorization-matrix baseline endpoints (permissions-api.md): list all
/// baselines and update one. Requires an authenticated session (401 if absent);
/// a P-07 actor attempting to put a CX-domain module into a baseline is rejected at
/// the data layer and surfaces here as 403. Errors use the API-05 envelope.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/persona-baselines")]
public sealed class PersonaBaselinesController : ControllerBase
{
    private readonly PersonaBaselineService _baselines;
    private readonly IPersonaBaselineService _personaBaselines;
    private readonly ISessionContextAccessor _sessionContext;
    private readonly ICurrentTenant _tenant;

    public PersonaBaselinesController(
        PersonaBaselineService baselines,
        IPersonaBaselineService personaBaselines,
        ISessionContextAccessor sessionContext,
        ICurrentTenant tenant)
    {
        _baselines = baselines;
        _personaBaselines = personaBaselines;
        _sessionContext = sessionContext;
        _tenant = tenant;
    }

    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct) =>
        InvokeAsync(async _ =>
        {
            var baselines = await _baselines.GetAllBaselinesAsync(_tenant.TenantId, ct);
            return Ok(new PersonaBaselineListResponse { Items = baselines.Select(ToResponse).ToList() });
        });

    [HttpPut("{personaId}")]
    public Task<IActionResult> Update(string personaId, [FromBody] UpdatePersonaBaselineRequest request, CancellationToken ct) =>
        InvokeAsync(async session =>
        {
            var assignments = request.PermissionModuleAssignments
                .Select(a => new PersonaModuleAssignment { ModuleId = a.ModuleId, AllowedModes = a.AllowedModes })
                .ToList();

            await _baselines.UpdateBaselineAsync(_tenant.TenantId, session.UserId, session.Persona, personaId, assignments, ct);

            var updated = await _personaBaselines.GetAsync(_tenant.TenantId, personaId, ct);
            return Ok(new UpdatePersonaBaselineResponse
            {
                BaselineId = updated?.BaselineId ?? Guid.Empty,
                PersonaId = personaId,
                IsCustomised = updated?.IsCustomised ?? true,
                UpdatedAt = updated?.UpdatedAt ?? default,
            });
        });

    private async Task<IActionResult> InvokeAsync(Func<SessionContext, Task<IActionResult>> action)
    {
        var session = _sessionContext.Current!;

        try
        {
            return await action(session);
        }
        catch (ForbiddenException ex)
        {
            return Error(StatusCodes.Status403Forbidden, ex.Code, ex.Message);
        }
    }

    private static PersonaBaselineResponse ToResponse(PersonaBaseline baseline) => new()
    {
        BaselineId = baseline.BaselineId,
        PersonaId = baseline.PersonaId,
        PermissionModuleAssignments = baseline.PermissionModuleAssignments
            .Select(a => new ModuleAssignmentDto { ModuleId = a.ModuleId, AllowedModes = a.AllowedModes })
            .ToList(),
        DefaultDataScopeRules = baseline.DefaultDataScopeRules,
        IsCustomised = baseline.IsCustomised,
        UpdatedAt = baseline.UpdatedAt,
    };

    private ObjectResult Error(int status, string code, string message) => StatusCode(status, new ApiErrorEnvelope
    {
        Error = new ApiErrorDetail
        {
            Code = code,
            Message = message,
            CorrelationId = HttpContext.TraceIdentifier,
        },
    });
}
