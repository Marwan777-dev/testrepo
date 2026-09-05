using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nabadat.UserManagement.Api.Contracts;
using Nabadat.UserManagement.Api.Interfaces;
using Nabadat.UserManagement.Application.Auth.Exceptions;
using Nabadat.UserManagement.Application.Interfaces;
using Nabadat.UserManagement.Application.Permissions;
using Nabadat.UserManagement.Application.Permissions.Exceptions;
using Nabadat.UserManagement.Application.Users;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.ValueObjects;
using Nabadat.UserManagement.Application.Permissions.Interfaces;
using Nabadat.UserManagement.Application.Users.Interfaces;

namespace Nabadat.UserManagement.Api.Controllers;

/// <summary>
/// Tenant user management endpoints (users-api.md). Every endpoint requires an authenticated
/// session: <c>[Authorize]</c> challenges with 401 (API-05 envelope) when the bearer token is
/// missing/invalid, and the PortalSession handler populates <see cref="ISessionContextAccessor"/>
/// with the actor. Authorization is enforced at the service/data layer — a
/// <see cref="ForbiddenException"/> surfaces as 403 (never 401). All non-2xx responses use the
/// API-05 error envelope.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/users")]
public sealed class UsersController : ControllerBase
{
    private readonly UserManagementService _users;
    private readonly PermissionAssignmentService _permissionAssignment;
    private readonly ITenantUserService _userRepository;
    private readonly IPermissionModuleAssignmentService _permissions;
    private readonly ISessionContextAccessor _sessionContext;
    private readonly ICurrentTenant _tenant;

    public UsersController(
        UserManagementService users,
        PermissionAssignmentService permissionAssignment,
        ITenantUserService userRepository,
        IPermissionModuleAssignmentService permissions,
        ISessionContextAccessor sessionContext,
        ICurrentTenant tenant)
    {
        _users = users;
        _permissionAssignment = permissionAssignment;
        _userRepository = userRepository;
        _permissions = permissions;
        _sessionContext = sessionContext;
        _tenant = tenant;
    }

    [HttpGet]
    public Task<IActionResult> List(
        [FromQuery(Name = "page_size")] int pageSize = 50,
        [FromQuery(Name = "page_token")] string? pageToken = null,
        [FromQuery] string? status = null,
        [FromQuery] string? persona = null,
        [FromQuery] string? q = null,
        CancellationToken ct = default) =>
        InvokeAsync(async session =>
        {
            _users.EnsureCanViewUsers(session.Persona);
            var page = await _userRepository.ListAsync(status, persona, q, pageSize, pageToken, ct);
            return Ok(new UserListResponse
            {
                Items = page.Items.Select(ToSummary).ToList(),
                NextPageToken = page.NextPageToken,
                TotalCount = page.TotalCount,
            });
        });

    [HttpPost]
    public Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken ct) =>
        InvokeAsync(async session =>
        {
            if (await _userRepository.ExistsAsync(request.Username, ct))
            {
                return Error(StatusCodes.Status409Conflict, "users.username_conflict", "A user with this username already exists.");
            }

            try
            {
                var user = await _users.CreateUserAsync(
                    _tenant.TenantId, session.UserId, session.Persona, request.Username, request.Persona, request.Password, ct);
                return StatusCode(StatusCodes.Status201Created, ToSummary(user));
            }
            catch (WeakPasswordException ex)
            {
                // Initial password fails FR-027 complexity — surface field-level codes.
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new ApiErrorEnvelope
                {
                    Error = new ApiErrorDetail
                    {
                        Code = "users.weak_password",
                        Message = ex.Message,
                        CorrelationId = HttpContext.TraceIdentifier,
                        Details = ex.Errors.Select(code => new ApiErrorFieldDetail { Field = "password", Code = code }).ToList(),
                    },
                });
            }
        });

    [HttpGet("{userId:guid}")]
    public Task<IActionResult> Get(Guid userId, CancellationToken ct) =>
        InvokeAsync(async session =>
        {
            _users.EnsureCanViewUsers(session.Persona);
            var user = await _userRepository.GetByIdAsync(userId, ct);
            if (user is null)
            {
                return Error(StatusCodes.Status404NotFound, "users.not_found", "User not found.");
            }

            var assignments = await _permissions.GetAssignmentsAsync(userId, ct);
            return Ok(new UserDetailResponse
            {
                UserId = user.UserId,
                Username = user.Username,
                Persona = user.Persona,
                Status = user.Status.ToWire(),
                IsMfaEnrolled = user.IsMfaEnrolled,
                OrganizationNodeId = user.OrganizationNodeId,
                LastPermissionSnapshotVersion = user.LastPermissionSnapshotVersion,
                PermissionModuleAssignments = assignments
                    .Select(a => new ModuleAssignmentDto { ModuleId = a.ModuleId, AllowedModes = a.AllowedModes })
                    .ToList(),
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
            });
        });

    [HttpPut("{userId:guid}")]
    public Task<IActionResult> Update(Guid userId, [FromBody] UpdateUserRequest request, CancellationToken ct) =>
        InvokeAsync(async session =>
        {
            await _users.UpdateProfileAsync(
                session.UserId, session.Persona, userId, request.Persona, request.OrganizationNodeId, ct);
            return NoContent();
        });

    [HttpPost("{userId:guid}/deactivate")]
    public Task<IActionResult> Deactivate(Guid userId, CancellationToken ct) =>
        InvokeAsync(async session =>
        {
            await _users.DeactivateUserAsync(session.UserId, session.Persona, userId, ct);
            return NoContent();
        });

    [HttpPost("{userId:guid}/reactivate")]
    public Task<IActionResult> Reactivate(Guid userId, CancellationToken ct) =>
        InvokeAsync(async session =>
        {
            await _users.ReactivateUserAsync(session.UserId, session.Persona, userId, ct);
            return NoContent();
        });

    [HttpPost("{userId:guid}/unlock")]
    public Task<IActionResult> Unlock(Guid userId, CancellationToken ct) =>
        InvokeAsync(async session =>
        {
            try
            {
                await _users.UnlockUserAsync(session.UserId, session.Persona, userId, ct);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Error(StatusCodes.Status409Conflict, "users.not_locked", ex.Message);
            }
        });

    [HttpPost("{userId:guid}/mfa-reset")]
    public Task<IActionResult> ResetMfa(Guid userId, CancellationToken ct) =>
        InvokeAsync(async session =>
        {
            await _users.AdminMfaResetAsync(session.UserId, session.Persona, userId, ct);
            return NoContent();
        });

    [HttpPost("{userId:guid}/password-reset")]
    public Task<IActionResult> ResetPassword(Guid userId, CancellationToken ct) =>
        InvokeAsync(async session =>
        {
            try
            {
                await _users.AdminPasswordResetAsync(session.UserId, session.Persona, userId, ct);
                return NoContent();
            }
            catch (InvalidOperationException)
            {
                // M-09 delivery failed; the unit of work rolled back, so no state changed.
                return Error(StatusCodes.Status503ServiceUnavailable, "users.notification_unavailable", "Notification delivery is temporarily unavailable.");
            }
        });

    [HttpPut("{userId:guid}/permissions")]
    public Task<IActionResult> ReplacePermissions(Guid userId, [FromBody] ReplacePermissionsRequest request, CancellationToken ct) =>
        InvokeAsync(async session =>
        {
            var assignments = request.Assignments
                .Select(a => new PermissionModuleAssignment { ModuleId = a.ModuleId, AllowedModes = a.AllowedModes })
                .ToList();
            await _permissionAssignment.ReplacePermissionsAsync(session.UserId, session.Persona, userId, assignments, ct);

            // Contract returns the surviving assignments (with their server-assigned ids).
            var persisted = await _permissions.GetAssignmentsAsync(userId, ct);
            return Ok(new ReplacePermissionsResponse
            {
                Assignments = persisted
                    .Select(a => new PermissionAssignmentDto
                    {
                        AssignmentId = a.AssignmentId,
                        ModuleId = a.ModuleId,
                        AllowedModes = a.AllowedModes,
                    })
                    .ToList(),
            });
        });

    /// <summary>
    /// Runs the action with the authenticated session ([Authorize] guarantees it is present) and
    /// maps the universal data-layer exceptions: <see cref="ForbiddenException"/> → 403, missing
    /// user → 404. Endpoint-specific conditions (409 not-locked, 503 M-09) are handled in-place.
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
        catch (KeyNotFoundException)
        {
            return Error(StatusCodes.Status404NotFound, "users.not_found", "User not found.");
        }
    }

    private static UserSummaryResponse ToSummary(TenantUser user) => new()
    {
        UserId = user.UserId,
        Username = user.Username,
        Persona = user.Persona,
        Status = user.Status.ToWire(),
        IsMfaEnrolled = user.IsMfaEnrolled,
        OrganizationNodeId = user.OrganizationNodeId,
        CreatedAt = user.CreatedAt,
        UpdatedAt = user.UpdatedAt,
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
