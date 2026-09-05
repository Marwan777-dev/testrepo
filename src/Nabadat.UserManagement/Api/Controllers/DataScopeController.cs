using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nabadat.UserManagement.Api.Contracts;
using Nabadat.UserManagement.Api.Interfaces;
using Nabadat.UserManagement.Application.Permissions;
using Nabadat.UserManagement.Application.Permissions.Exceptions;
using Nabadat.UserManagement.Application.Users;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.ValueObjects;
using Nabadat.UserManagement.Application.Permissions.Interfaces;
using Nabadat.UserManagement.Application.Users.Interfaces;

namespace Nabadat.UserManagement.Api.Controllers;

/// <summary>
/// Data-scope and custom-rule endpoints (permissions-api.md, US3, T106): read/replace a
/// user's parameter scope + hierarchy node, CRUD their custom authorization rules, and
/// ingest M-13 parameter definitions. The user-facing endpoints require an authenticated
/// P-01/P-07 session (<c>UserManagement.View</c>/<c>Manage</c>, enforced via
/// <see cref="UserCreationPolicy"/>); the parameter-ingestion endpoint is an internal
/// service call with no user session. A <see cref="ValidationException"/> surfaces as 422
/// (scope assignment) or 400 (parameter ingestion) with the field-level API-05 details;
/// <see cref="ForbiddenException"/> → 403; a missing user/rule → 404.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1")]
public sealed class DataScopeController : ControllerBase
{
    private readonly UserCreationPolicy _authority;
    private readonly DataScopeRuleService _dataScope;
    private readonly CustomAuthorizationRuleService _customRules;
    private readonly M13ParameterContractAdapter _parameterAdapter;
    private readonly IDataScopeService _scopes;
    private readonly ICustomAuthorizationRuleService _ruleRepository;
    private readonly ITenantUserService _users;
    private readonly ISessionContextAccessor _sessionContext;

    public DataScopeController(
        UserCreationPolicy authority,
        DataScopeRuleService dataScope,
        CustomAuthorizationRuleService customRules,
        M13ParameterContractAdapter parameterAdapter,
        IDataScopeService scopes,
        ICustomAuthorizationRuleService ruleRepository,
        ITenantUserService users,
        ISessionContextAccessor sessionContext)
    {
        _authority = authority;
        _dataScope = dataScope;
        _customRules = customRules;
        _parameterAdapter = parameterAdapter;
        _scopes = scopes;
        _ruleRepository = ruleRepository;
        _users = users;
        _sessionContext = sessionContext;
    }

    [HttpGet("users/{userId:guid}/scope")]
    public Task<IActionResult> GetScope(Guid userId, CancellationToken ct) =>
        InvokeAsync(async session =>
        {
            _authority.EnsureCanViewUsers(session.Persona);
            var user = await _users.GetByIdAsync(userId, ct);
            if (user is null)
            {
                return Error(StatusCodes.Status404NotFound, "users.not_found", "User not found.");
            }

            return Ok(await BuildScopeResponseAsync(user, ct));
        });

    [HttpPut("users/{userId:guid}/scope")]
    public Task<IActionResult> ReplaceScope(Guid userId, [FromBody] UpdateUserScopeRequest request, CancellationToken ct) =>
        InvokeAsync(async session =>
        {
            _authority.EnsureCanManageUsers(session.Persona);

            var assignments = request.DataScopeAssignments
                .Select(a => new DataScopeAssignment { ParameterName = a.ParameterName, AllowedValues = a.AllowedValues })
                .ToList();

            await _dataScope.ReplaceUserScopeAsync(
                session.UserId, session.Persona, userId, request.OrganizationNodeId, assignments, ct);

            var user = await _users.GetByIdAsync(userId, ct);
            return user is null
                ? Error(StatusCodes.Status404NotFound, "users.not_found", "User not found.")
                : Ok(await BuildScopeResponseAsync(user, ct));
        });

    [HttpPost("users/{userId:guid}/custom-rules")]
    public Task<IActionResult> CreateCustomRule(Guid userId, [FromBody] CreateCustomRuleRequest request, CancellationToken ct) =>
        InvokeAsync(async session =>
        {
            _authority.EnsureCanManageUsers(session.Persona);
            var rule = await _customRules.CreateRuleAsync(
                session.UserId, session.Persona, userId, request.AllowedActions, request.ParameterScopeAssignments, ct);
            return StatusCode(StatusCodes.Status201Created, ToRuleResponse(rule));
        });

    [HttpPut("users/{userId:guid}/custom-rules/{ruleId:guid}")]
    public Task<IActionResult> UpdateCustomRule(Guid userId, Guid ruleId, [FromBody] UpdateCustomRuleRequest request, CancellationToken ct) =>
        InvokeAsync(async session =>
        {
            _authority.EnsureCanManageUsers(session.Persona);
            var rule = await _customRules.UpdateRuleAsync(
                session.UserId, session.Persona, userId, ruleId, request.AllowedActions, request.ParameterScopeAssignments, ct);
            return Ok(ToRuleResponse(rule));
        });

    [HttpDelete("users/{userId:guid}/custom-rules/{ruleId:guid}")]
    public Task<IActionResult> DeleteCustomRule(Guid userId, Guid ruleId, CancellationToken ct) =>
        InvokeAsync(async session =>
        {
            _authority.EnsureCanManageUsers(session.Persona);
            await _customRules.DeleteRuleAsync(session.UserId, session.Persona, userId, ruleId, ct);
            return NoContent();
        });

    /// <summary>
    /// Internal service ingestion of M-13 parameter definitions — no user session
    /// (validated by service identity at the gateway). Validation failures map to 400.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("authorization/scope/parameters")]
    public async Task<IActionResult> StoreParameters([FromBody] M13ParameterPayload payload, CancellationToken ct)
    {
        try
        {
            await _parameterAdapter.StoreParameterDefinitionsAsync(payload, ct);
            return Ok();
        }
        catch (ValidationException ex)
        {
            return ValidationError(StatusCodes.Status400BadRequest, "scope.invalid_parameter_definition", ex);
        }
    }

    private async Task<UserScopeResponse> BuildScopeResponseAsync(TenantUser user, CancellationToken ct)
    {
        var assignments = await _scopes.GetScopeAssignmentsAsync(user.UserId, ct);
        var rules = await _ruleRepository.GetRulesForUserAsync(user.UserId, ct);

        return new UserScopeResponse
        {
            OrganizationNodeId = user.OrganizationNodeId,
            DataScopeAssignments = assignments
                .Select(a => new DataScopeAssignmentDto { ParameterName = a.ParameterName, AllowedValues = a.AllowedValues })
                .ToList(),
            CustomRules = rules
                .Select(r => new CustomRuleDto
                {
                    RuleId = r.RuleId,
                    AllowedActions = r.AllowedActions,
                    ParameterScopeAssignments = r.ParameterScopeAssignments,
                })
                .ToList(),
        };
    }

    private static CustomRuleResponse ToRuleResponse(CustomAuthorizationRule rule) => new()
    {
        RuleId = rule.RuleId,
        AllowedActions = rule.AllowedActions,
        ParameterScopeAssignments = rule.ParameterScopeAssignments,
        CreatedAt = rule.CreatedAt,
    };

    /// <summary>
    /// Resolves the session (401 if absent) and maps the data-layer exceptions:
    /// <see cref="ForbiddenException"/> → 403, missing user/rule → 404, and an
    /// invalid scope assignment (<see cref="ValidationException"/>) → 422.
    /// </summary>
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
        catch (ValidationException ex)
        {
            return ValidationError(StatusCodes.Status422UnprocessableEntity, "scope.invalid_assignment", ex);
        }
        catch (KeyNotFoundException)
        {
            return Error(StatusCodes.Status404NotFound, "users.not_found", "User or rule not found.");
        }
    }

    private ObjectResult Error(int status, string code, string message) => StatusCode(status, new ApiErrorEnvelope
    {
        Error = new ApiErrorDetail
        {
            Code = code,
            Message = message,
            CorrelationId = HttpContext.TraceIdentifier,
        },
    });

    private ObjectResult ValidationError(int status, string code, ValidationException ex) => StatusCode(status, new ApiErrorEnvelope
    {
        Error = new ApiErrorDetail
        {
            Code = code,
            Message = ex.Message,
            CorrelationId = HttpContext.TraceIdentifier,
            Details = ex.Failures.Select(f => new ApiErrorFieldDetail { Field = f.Field, Code = f.Code }).ToList(),
        },
    });
}
