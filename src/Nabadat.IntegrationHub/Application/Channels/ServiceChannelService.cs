using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nabadat.IntegrationHub.Application.Channels.Dtos;
using Nabadat.IntegrationHub.Application.Channels.Interfaces;
using Nabadat.IntegrationHub.Application.Interfaces;
using Nabadat.IntegrationHub.Domain.Entities;

namespace Nabadat.IntegrationHub.Application.Channels;

/// <summary>
/// T034 — the service-channel aggregate (US1). Composes the five US1 rules
/// (<see cref="ChannelIdSanitizer"/>, <see cref="ChannelNameValidator"/>,
/// <see cref="ChannelIdUniquenessValidator"/>, <see cref="ChannelIdLockGuard"/>,
/// <see cref="ParameterContractDependencyRule"/>) and persists itself through
/// <see cref="ITenantDbContext"/> — the context <b>is</b> the unit of work, there is no repository layer
/// (DB-08 / AMENDMENT-007).
///
/// <para>Every write runs inside <see cref="ITenantDbContext.ExecuteAsync(Func{Task}, CancellationToken)"/>
/// because each one spans more than one table: the channel row, its contract rows, and the M-17
/// <c>event_log</c> row all commit or roll back together.</para>
///
/// <para><b>No delete path exists</b> (BR-07 / FR-S3-02) — deactivation is the only removal.</para>
///
/// <para>Timestamps come from the injected <see cref="TimeProvider"/> (DB-08 rule 7); there is no
/// <c>DateTime.UtcNow</c> anywhere in this module.</para>
/// </summary>
public sealed class ServiceChannelService : IServiceChannelService
{
    /// <summary>
    /// VR-F13 / NFR-16 — a tenant may hold at most 100 service channels. This ceiling is also what makes the
    /// list read below safe to materialise in full before slicing the cursor page.
    /// </summary>
    public const int MaxChannelsPerTenant = 100;

    private const int MaxPageSize = 200;
    private const int DefaultPageSize = 50;

    private readonly ITenantDbContext _db;
    private readonly ChannelIdSanitizer _sanitizer;
    private readonly ChannelNameValidator _nameValidator;
    private readonly ChannelIdUniquenessValidator _idUniqueness;
    private readonly ChannelIdLockGuard _lockGuard;
    private readonly ParameterContractDependencyRule _contractRule;
    private readonly TimeProvider _time;

    public ServiceChannelService(
        ITenantDbContext db,
        ChannelIdSanitizer sanitizer,
        ChannelNameValidator nameValidator,
        ChannelIdUniquenessValidator idUniqueness,
        ChannelIdLockGuard lockGuard,
        ParameterContractDependencyRule contractRule,
        TimeProvider time)
    {
        _db = db;
        _sanitizer = sanitizer;
        _nameValidator = nameValidator;
        _idUniqueness = idUniqueness;
        _lockGuard = lockGuard;
        _contractRule = contractRule;
        _time = time;
    }

    /// <inheritdoc />
    public async Task<ServiceChannelSaveResult> CreateAsync(
        ServiceChannelCreateCommand command,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var errors = new List<ChannelValidationError>();

        // The client sanitises live as the user types (AC-S4-01), but the server is the authority: a raw
        // "My kiosk #1" from any caller becomes "Mykiosk1" here rather than tripping the column CHECK.
        var channelId = _sanitizer.Sanitize(command.ChannelId);

        var existing = await _db.ServiceChannels
            .AsNoTracking()
            .Select(c => new { c.NameEn, c.ChannelId })
            .ToListAsync(ct);

        if (existing.Count >= MaxChannelsPerTenant)
        {
            // VR-F13 is a create-time guardrail only — an existing over-limit tenant can still edit.
            return ServiceChannelSaveResult.Failed(
                ChannelErrorCodes.CapacityExceeded,
                $"You've reached the limit of {MaxChannelsPerTenant} service channels for this tenant.");
        }

        errors.AddRange(_nameValidator
            .Validate(command.NameEn, command.NameAr, existing.Select(c => c.NameEn)).Errors);
        errors.AddRange(_idUniqueness
            .Validate(existing.Select(c => c.ChannelId), channelId).Errors);

        var contract = NormaliseContract(command.Contract);
        errors.AddRange(await ValidateContractParametersAsync(contract, ct));

        if (errors.Count > 0)
        {
            return ServiceChannelSaveResult.Failed(errors);
        }

        var now = _time.GetUtcNow();
        var channel = new ServiceChannel
        {
            Id = Guid.NewGuid(),
            NameEn = command.NameEn!.Trim(),
            NameAr = command.NameAr!.Trim(),
            ChannelId = channelId,
            Description = Normalise(command.Description),
            Active = command.Active,
            ChannelIdLocked = false,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _db.ExecuteAsync(async () =>
        {
            _db.ServiceChannels.Add(channel);

            foreach (var row in contract)
            {
                _db.ChannelParameterAssignments.Add(new ChannelParameterAssignment
                {
                    ServiceChannelId = channel.Id,
                    ParameterId = row.ParameterId,
                    Supported = row.Supported,
                    Required = row.Required,
                });
            }

            Audit("channel.created", channel, command.ActorId, command.ActorPersona, now, oldValue: null,
                newValue: Snapshot(channel, contract));

            await Task.CompletedTask;
        }, ct);

        return ServiceChannelSaveResult.Ok(await ProjectAsync(channel.Id, ct)
            ?? throw new InvalidOperationException("The created service channel could not be read back."));
    }

    /// <inheritdoc />
    public async Task<ServiceChannelSaveResult> UpdateAsync(
        Guid id,
        ServiceChannelUpdateCommand command,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var channel = await _db.ServiceChannels.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (channel is null)
        {
            return ServiceChannelSaveResult.Failed(ChannelErrorCodes.ChannelNotFound, "Service channel not found");
        }

        var before = Snapshot(channel, await ReadContractAsync(id, ct));
        var errors = new List<ChannelValidationError>();

        // A null ChannelId means the client did not submit the field — which a locked channel's read-only
        // form legitimately does — so the persisted value stands. A submitted-but-unusable value is an error.
        string? requestedChannelId = null;
        if (command.ChannelId is not null)
        {
            requestedChannelId = _sanitizer.Sanitize(command.ChannelId);
            if (requestedChannelId.Length == 0)
            {
                errors.Add(new ChannelValidationError(
                    ChannelErrorCodes.ChannelIdRequired, "Service channel ID is required", ChannelFields.ChannelId));
                requestedChannelId = null;
            }
        }

        var lockOutcome = _lockGuard.ValidateIdChange(
            channel, await HasLoggedSuccessfulRequestAsync(id, ct), requestedChannelId);
        errors.AddRange(lockOutcome.Errors);

        var others = await _db.ServiceChannels
            .AsNoTracking()
            .Where(c => c.Id != id)
            .Select(c => new { c.NameEn, c.ChannelId })
            .ToListAsync(ct);

        errors.AddRange(_nameValidator
            .Validate(command.NameEn, command.NameAr, others.Select(c => c.NameEn)).Errors);

        if (lockOutcome.IsValid && requestedChannelId is not null)
        {
            errors.AddRange(_idUniqueness
                .Validate(others.Select(c => c.ChannelId), requestedChannelId).Errors);
        }

        var contract = NormaliseContract(command.Contract);
        errors.AddRange(await ValidateContractParametersAsync(contract, ct));

        if (errors.Count > 0)
        {
            return ServiceChannelSaveResult.Failed(errors);
        }

        var now = _time.GetUtcNow();
        var previousChannelId = channel.ChannelId;
        var wasActive = channel.Active;

        await _db.ExecuteAsync(async () =>
        {
            channel.NameEn = command.NameEn!.Trim();
            channel.NameAr = command.NameAr!.Trim();
            channel.Description = Normalise(command.Description);
            channel.Active = command.Active;
            channel.UpdatedAt = now;

            if (requestedChannelId is not null)
            {
                channel.ChannelId = requestedChannelId;
            }

            // The submitted contract is authoritative and replaces the stored one wholesale: a row the
            // client omitted means "no longer assigned", which is how SCR-04's table expresses removal.
            var stored = await _db.ChannelParameterAssignments
                .Where(a => a.ServiceChannelId == id)
                .ToListAsync(ct);
            _db.ChannelParameterAssignments.RemoveRange(stored);

            foreach (var row in contract)
            {
                _db.ChannelParameterAssignments.Add(new ChannelParameterAssignment
                {
                    ServiceChannelId = id,
                    ParameterId = row.ParameterId,
                    Supported = row.Supported,
                    Required = row.Required,
                });
            }

            var after = Snapshot(channel, contract);
            Audit("channel.updated", channel, command.ActorId, command.ActorPersona, now, before, after);

            // The two transition-specific events contracts/api-endpoints.md requires alongside the generic
            // update, so an auditor can find an endpoint-path change or a status flip without diffing payloads.
            if (!string.Equals(previousChannelId, channel.ChannelId, StringComparison.Ordinal))
            {
                Audit("channel.id_changed", channel, command.ActorId, command.ActorPersona, now,
                    JsonSerializer.Serialize(new { channel_id = previousChannelId }),
                    JsonSerializer.Serialize(new { channel_id = channel.ChannelId }));
            }

            if (wasActive != channel.Active)
            {
                Audit(channel.Active ? "channel.activated" : "channel.deactivated",
                    channel, command.ActorId, command.ActorPersona, now,
                    JsonSerializer.Serialize(new { active = wasActive }),
                    JsonSerializer.Serialize(new { active = channel.Active }));
            }
        }, ct);

        return ServiceChannelSaveResult.Ok(await ProjectAsync(id, ct)
            ?? throw new InvalidOperationException("The updated service channel could not be read back."));
    }

    /// <inheritdoc />
    public async Task<ServiceChannelPage> ListAsync(
        string? cursor = null,
        int limit = DefaultPageSize,
        CancellationToken ct = default)
    {
        var pageSize = limit is < 1 or > MaxPageSize ? DefaultPageSize : limit;

        // Ordered by EN name case-insensitively, then id, so the order is stable across pages. Materialising
        // the whole list first is safe and deliberate: VR-F13 caps a tenant at MaxChannelsPerTenant rows, and
        // no channel is ever deleted (BR-07), so the cursor row cannot vanish mid-pagination.
        var ordered = await _db.ServiceChannels
            .AsNoTracking()
            .OrderBy(c => c.NameEn.ToLower())
            .ThenBy(c => c.Id)
            .ToListAsync(ct);

        var start = 0;
        if (!string.IsNullOrEmpty(cursor) && Guid.TryParseExact(cursor, "N", out var afterId))
        {
            var index = ordered.FindIndex(c => c.Id == afterId);
            start = index < 0 ? 0 : index + 1;
        }

        var page = ordered.Skip(start).Take(pageSize).ToList();
        var nextCursor = start + page.Count < ordered.Count && page.Count > 0
            ? page[^1].Id.ToString("N")
            : null;

        var ids = page.Select(c => c.Id).ToList();

        var contractCounts = await _db.ChannelParameterAssignments
            .AsNoTracking()
            .Where(a => ids.Contains(a.ServiceChannelId))
            .GroupBy(a => a.ServiceChannelId)
            .Select(g => new
            {
                ServiceChannelId = g.Key,
                Supported = g.Count(a => a.Supported),
                Required = g.Count(a => a.Required),
            })
            .ToListAsync(ct);

        var integrationCounts = await _db.Integrations
            .AsNoTracking()
            .Where(i => ids.Contains(i.ServiceChannelId))
            .GroupBy(i => i.ServiceChannelId)
            .Select(g => new { ServiceChannelId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var items = page
            .Select(channel =>
            {
                var counts = contractCounts.FirstOrDefault(c => c.ServiceChannelId == channel.Id);
                var integrations = integrationCounts.FirstOrDefault(c => c.ServiceChannelId == channel.Id);
                return Map(
                    channel,
                    counts?.Supported ?? 0,
                    counts?.Required ?? 0,
                    integrations?.Count ?? 0,
                    Array.Empty<ChannelContractRowDto>());
            })
            .ToList();

        return new ServiceChannelPage(items, nextCursor);
    }

    /// <inheritdoc />
    public Task<ServiceChannelDto?> GetAsync(Guid id, CancellationToken ct = default) => ProjectAsync(id, ct);

    // ── internals ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Normalises the submitted contract: applies FR-S4-04's Supported→Required dependency, drops rows where
    /// both flags ended up false (they carry no signal, and storing 23 empty rows per channel would make the
    /// counts and the table meaningless), and de-duplicates by parameter so a repeated row cannot violate the
    /// composite primary key.
    /// </summary>
    private List<ChannelParameterAssignmentInput> NormaliseContract(
        IReadOnlyList<ChannelParameterAssignmentInput>? submitted) =>
        _contractRule.ApplyAll(submitted)
            .Where(row => row.Supported || row.Required)
            .GroupBy(row => row.ParameterId)
            .Select(group => group.First())
            .ToList();

    /// <summary>Rejects contract rows pointing at parameters that are not in the catalogue.</summary>
    private async Task<IReadOnlyList<ChannelValidationError>> ValidateContractParametersAsync(
        IReadOnlyList<ChannelParameterAssignmentInput> contract,
        CancellationToken ct)
    {
        if (contract.Count == 0)
        {
            return Array.Empty<ChannelValidationError>();
        }

        var submittedIds = contract.Select(row => row.ParameterId).ToList();
        var known = await _db.Parameters
            .AsNoTracking()
            .Where(p => submittedIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(ct);

        return submittedIds
            .Except(known)
            .Select(unknown => new ChannelValidationError(
                ChannelErrorCodes.UnknownParameter, $"Parameter {unknown} does not exist", ChannelFields.Contract))
            .ToList();
    }

    /// <summary>
    /// BR-05's live lock probe: has any integration on this channel logged a 2xx? Complements the persisted
    /// <see cref="ServiceChannel.ChannelIdLocked"/> flag (which US4's pipeline sets) so a channel with traffic
    /// but an unwritten flag is still treated as locked.
    /// </summary>
    private async Task<bool> HasLoggedSuccessfulRequestAsync(Guid channelId, CancellationToken ct)
    {
        var integrationIds = _db.Integrations
            .Where(i => i.ServiceChannelId == channelId)
            .Select(i => i.Id);

        return await _db.IntegrationRequestLogs
            .AsNoTracking()
            .AnyAsync(
                log => log.IntegrationId != null
                    && integrationIds.Contains(log.IntegrationId.Value)
                    && log.HttpStatus >= 200
                    && log.HttpStatus < 300,
                ct);
    }

    /// <summary>Reads one channel with its full contract and the three SCR-03 counts.</summary>
    private async Task<ServiceChannelDto?> ProjectAsync(Guid id, CancellationToken ct)
    {
        var channel = await _db.ServiceChannels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (channel is null)
        {
            return null;
        }

        var contract = await ReadContractAsync(id, ct);
        var integrations = await _db.Integrations.AsNoTracking().CountAsync(i => i.ServiceChannelId == id, ct);

        return Map(
            channel,
            contract.Count(row => row.Supported),
            contract.Count(row => row.Required),
            integrations,
            contract);
    }

    /// <summary>
    /// Reads a channel's contract rows joined to the parameter catalogue, in API-field order.
    ///
    /// <para>The <c>OrderBy</c> deliberately targets the joined <c>Parameter.ApiField</c> and the DTO
    /// projection comes <b>last</b>: ordering by a property of an already-projected record is not translatable
    /// (EF throws rather than silently evaluating it client-side), so the order of these operators is load-bearing,
    /// not stylistic.</para>
    /// </summary>
    private async Task<List<ChannelContractRowDto>> ReadContractAsync(Guid channelId, CancellationToken ct) =>
        await _db.ChannelParameterAssignments
            .AsNoTracking()
            .Where(a => a.ServiceChannelId == channelId)
            .Join(
                _db.Parameters.AsNoTracking(),
                a => a.ParameterId,
                p => p.Id,
                (a, p) => new { Assignment = a, Parameter = p })
            .OrderBy(joined => joined.Parameter.ApiField)
            .Select(joined => new ChannelContractRowDto(
                joined.Parameter.Id,
                joined.Parameter.ApiField,
                joined.Parameter.NameEn,
                joined.Parameter.NameAr,
                joined.Assignment.Supported,
                joined.Assignment.Required))
            .ToListAsync(ct);

    private static ServiceChannelDto Map(
        ServiceChannel channel,
        int supportedCount,
        int requiredCount,
        int integrationsCount,
        IReadOnlyList<ChannelContractRowDto> contract) =>
        new(
            channel.Id,
            channel.NameEn,
            channel.NameAr,
            channel.ChannelId,
            channel.Description,
            channel.Active,
            channel.ChannelIdLocked,
            supportedCount,
            requiredCount,
            integrationsCount,
            contract,
            channel.CreatedAt,
            channel.UpdatedAt);

    /// <summary>
    /// Appends the M-17 audit row for a channel change. Tracked on the <b>same</b> context as the change, so
    /// the enclosing <c>ExecuteAsync</c> commits both together or neither (DB-08).
    ///
    /// <para>TODO(US9): T145's <c>AuditEventEmitter</c> takes this over and adds the correlation id — the
    /// column is nullable, and no correlation source is wired into this module yet.</para>
    /// </summary>
    private void Audit(
        string eventType,
        ServiceChannel channel,
        Guid actorId,
        string? actorPersona,
        DateTimeOffset occurredAt,
        string? oldValue,
        string? newValue) =>
        _db.EventLogs.Add(new EventLog
        {
            EventId = Guid.NewGuid(),
            EventType = eventType,
            ActorId = actorId == Guid.Empty ? null : actorId,
            ActorPersona = actorPersona,
            EntityType = nameof(ServiceChannel),
            EntityId = channel.Id,
            OldValue = oldValue,
            NewValue = newValue,
            OccurredAtUtc = occurredAt,
            CorrelationId = null,
        });

    private static string Snapshot(ServiceChannel channel, IReadOnlyList<ChannelContractRowDto> contract) =>
        JsonSerializer.Serialize(new
        {
            name_en = channel.NameEn,
            name_ar = channel.NameAr,
            channel_id = channel.ChannelId,
            description = channel.Description,
            active = channel.Active,
            supported_count = contract.Count(row => row.Supported),
            required_count = contract.Count(row => row.Required),
        });

    private static string Snapshot(ServiceChannel channel, IReadOnlyList<ChannelParameterAssignmentInput> contract) =>
        JsonSerializer.Serialize(new
        {
            name_en = channel.NameEn,
            name_ar = channel.NameAr,
            channel_id = channel.ChannelId,
            description = channel.Description,
            active = channel.Active,
            supported_count = contract.Count(row => row.Supported),
            required_count = contract.Count(row => row.Required),
        });

    private static string? Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
