using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nabadat.UserManagement.Api.Contracts;
using Nabadat.UserManagement.Api.Interfaces;
using Nabadat.UserManagement.Application.Permissions.Exceptions;
using Nabadat.UserManagement.Application.Users;
using Nabadat.UserManagement.Domain.Interfaces;
using Nabadat.UserManagement.Domain.ValueObjects;
using Nabadat.UserManagement.Application.Users.Interfaces;

namespace Nabadat.UserManagement.Api.Controllers;

/// <summary>
/// Read-only tenant audit trail (US4, permissions-api.md). <c>GET /api/v1/audit-log</c>
/// returns the tenant's M-10 audit events, cursor-paginated and filterable. M-10 owns the
/// full audit cycle — it writes events to <c>event_log</c> and reads them back through
/// <see cref="IAuditLogReader"/> (no external M-17 dependency; resolves gap-analysis
/// I-02/I-04). The endpoint exposes no write verbs — audit records are immutable. Access is
/// gated to P-01/P-07 by the same <c>UserManagement.View</c> check the user directory uses;
/// a <see cref="ForbiddenException"/> surfaces as 403, a missing session as 401 (via
/// <c>[Authorize]</c>), both using the API-05 error envelope.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/audit-log")]
public sealed class AuditLogController : ControllerBase
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;
    private const string ErasedActorPlaceholder = "[erased]";

    private readonly IAuditLogReader _auditLog;
    private readonly UserManagementService _users;
    private readonly ITenantUserService _userRepository;
    private readonly ISessionContextAccessor _sessionContext;

    public AuditLogController(
        IAuditLogReader auditLog,
        UserManagementService users,
        ITenantUserService userRepository,
        ISessionContextAccessor sessionContext)
    {
        _auditLog = auditLog;
        _users = users;
        _userRepository = userRepository;
        _sessionContext = sessionContext;
    }

    [HttpGet]
    public Task<IActionResult> List(
        [FromQuery(Name = "page_size")] int pageSize = DefaultPageSize,
        [FromQuery(Name = "page_token")] string? pageToken = null,
        [FromQuery(Name = "event_type")] string? eventType = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery(Name = "actor_id")] Guid? actorId = null,
        [FromQuery(Name = "entity_id")] Guid? entityId = null,
        CancellationToken ct = default) =>
        InvokeAsync(async session =>
        {
            // UserManagement.View — P-01/P-07 only; non-admin personas get 403.
            _users.EnsureCanViewUsers(session.Persona);

            if (pageSize is < 1 or > MaxPageSize)
            {
                return Error(
                    StatusCodes.Status400BadRequest,
                    "audit_log.invalid_page_size",
                    $"page_size must be between 1 and {MaxPageSize}.");
            }

            var filter = new AuditLogFilter
            {
                EventType = eventType,
                FromUtc = from,
                ToUtc = to,
                ActorId = actorId,
                EntityId = entityId,
            };

            var page = await _auditLog.QueryEventsAsync(filter, pageSize, pageToken, ct);
            var usernames = await ResolveActorUsernamesAsync(page.Items, ct);

            return Ok(new AuditLogResponse
            {
                Items = page.Items.Select(entry => ToResponse(entry, usernames)).ToList(),
                NextPageToken = page.NextCursor,
            });
        });

    /// <summary>
    /// Resolves each distinct actor id to its username once. A missing or erased actor
    /// (GP-03 nulls the username) maps to <see cref="ErasedActorPlaceholder"/>.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, string>> ResolveActorUsernamesAsync(
        IReadOnlyList<AuditLogEntry> items,
        CancellationToken ct)
    {
        var usernames = new Dictionary<Guid, string>();
        foreach (var id in items.Where(e => e.ActorId is not null).Select(e => e.ActorId!.Value).Distinct())
        {
            var user = await _userRepository.GetByIdAsync(id, ct);
            usernames[id] = string.IsNullOrEmpty(user?.Username) ? ErasedActorPlaceholder : user.Username;
        }

        return usernames;
    }

    private static AuditLogEntryResponse ToResponse(AuditLogEntry entry, IReadOnlyDictionary<Guid, string> usernames) => new()
    {
        EventId = entry.EventId,
        EventType = entry.EventType,
        ActorId = entry.ActorId,
        ActorUsername = entry.ActorId is { } id && usernames.TryGetValue(id, out var name) ? name : null,
        EntityType = entry.EntityType,
        EntityId = entry.EntityId,
        OldValue = ParseJson(entry.OldValueJson),
        NewValue = ParseJson(entry.NewValueJson),
        OccurredAtUtc = entry.OccurredAtUtc,
        CorrelationId = entry.CorrelationId,
    };

    /// <summary>Re-hydrates a stored jsonb payload as a JSON object; null/blank or unparseable → null.</summary>
    private static JsonNode? ParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

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
