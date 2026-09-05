using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nabadat.IntegrationHub.Application.Interfaces;
using Nabadat.IntegrationHub.Application.Parameters.Dtos;
using Nabadat.IntegrationHub.Application.Parameters.Interfaces;
using Nabadat.IntegrationHub.Domain.Entities;
using Nabadat.IntegrationHub.Domain.Interfaces;
using Nabadat.IntegrationHub.Domain.ValueObjects;

namespace Nabadat.IntegrationHub.Application.Parameters;

/// <summary>
/// T057 — the parameter-catalogue aggregate (US2). Composes the six US2 rules
/// (<see cref="ApiFieldNameSuggester"/> is client-side only and therefore not among them:
/// <see cref="ParameterNameValidator"/>, <see cref="ApiFieldNameUniquenessValidator"/>,
/// <see cref="ApiFieldNameLockGuard"/>, <see cref="RangeConfigValidator"/>,
/// <see cref="ParameterDisableImpactScanner"/>, <see cref="BuiltInParameterGuard"/>) plus BR-27's
/// <see cref="MappingSupportPolicy"/>, and persists itself through <see cref="ITenantDbContext"/> — the context
/// <b>is</b> the unit of work (DB-08 / AMENDMENT-007).
///
/// <para>Every write runs inside <see cref="ITenantDbContext.ExecuteAsync(Func{Task}, CancellationToken)"/>
/// because each spans more than one table: the parameter row, its channel assignments, and the M-17
/// <c>event_log</c> row all commit or roll back together.</para>
///
/// <para>The M-10 data-scope push (<see cref="DataScopeContractPublisher"/>) runs <b>after</b> the commit, not
/// inside it. It is a projection of data M-13 already owns, so an unreachable M-10 must not roll back the
/// tenant's own parameter — see that class's remarks.</para>
///
/// <para><b>No delete path exists</b> (BR-09) — disabling is the only removal, and a disabled parameter keeps its
/// API field name reserved forever (VR-F06).</para>
/// </summary>
public sealed class ParameterService : IParameterService
{
    /// <summary>
    /// VR-F13 / NFR-16 — a tenant may hold at most 200 <b>custom</b> parameters. The 23 seeded built-ins do not
    /// count toward the ceiling (contracts/api-endpoints.md).
    /// </summary>
    public const int MaxCustomParametersPerTenant = 200;

    private const int MaxPageSize = 200;
    private const int DefaultPageSize = 50;

    private readonly ITenantDbContext _db;
    private readonly ParameterNameValidator _nameValidator;
    private readonly ApiFieldNameUniquenessValidator _apiFieldUniqueness;
    private readonly ApiFieldNameLockGuard _lockGuard;
    private readonly RangeConfigValidator _rangeValidator;
    private readonly ParameterDisableImpactScanner _impactScanner;
    private readonly BuiltInParameterGuard _builtInGuard;
    private readonly IExternalParameterReferenceReader _externalReferences;
    private readonly DataScopeContractPublisher _dataScope;
    private readonly TimeProvider _time;

    public ParameterService(
        ITenantDbContext db,
        ParameterNameValidator nameValidator,
        ApiFieldNameUniquenessValidator apiFieldUniqueness,
        ApiFieldNameLockGuard lockGuard,
        RangeConfigValidator rangeValidator,
        ParameterDisableImpactScanner impactScanner,
        BuiltInParameterGuard builtInGuard,
        IExternalParameterReferenceReader externalReferences,
        DataScopeContractPublisher dataScope,
        TimeProvider time)
    {
        _db = db;
        _nameValidator = nameValidator;
        _apiFieldUniqueness = apiFieldUniqueness;
        _lockGuard = lockGuard;
        _rangeValidator = rangeValidator;
        _impactScanner = impactScanner;
        _builtInGuard = builtInGuard;
        _externalReferences = externalReferences;
        _dataScope = dataScope;
        _time = time;
    }

    /// <inheritdoc />
    public async Task<ParameterSaveResult> CreateAsync(ParameterCreateCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var errors = new List<ParameterValidationError>();

        if (!Enum.IsDefined(command.DataType))
        {
            // FR-F0-04 / [PO-G17]: the type list is closed. An out-of-range value is a malformed submission, and
            // failing fast here keeps every rule below (which switches on the type) working from a real member.
            return ParameterSaveResult.Failed(
                ParameterErrorCodes.InvalidDataType,
                "Select one of the supported parameter types",
                ParameterFields.DataType);
        }

        var customCount = await _db.Parameters
            .AsNoTracking()
            .CountAsync(p => p.Origin == ParameterOrigin.Custom, ct);

        if (customCount >= MaxCustomParametersPerTenant)
        {
            // VR-F13 is a create-time guardrail only — an existing over-limit tenant can still edit.
            return ParameterSaveResult.Failed(
                ParameterErrorCodes.CapacityExceeded,
                $"You've reached the limit of {MaxCustomParametersPerTenant} custom parameters for this tenant.");
        }

        // VR-F06: EVERY existing field name, built-in and disabled included. Filtering this list would be the bug.
        var existingApiFields = await _db.Parameters
            .AsNoTracking()
            .Select(p => p.ApiField)
            .ToListAsync(ct);

        errors.AddRange(_nameValidator.Validate(command.NameEn, command.NameAr).Errors);
        errors.AddRange(_apiFieldUniqueness.Validate(existingApiFields, command.ApiField).Errors);
        errors.AddRange(_rangeValidator
            .Validate(command.DataType, command.RangeMin, command.RangeMax, command.RangeUnit).Errors);

        var channelIds = Distinct(command.ChannelIds);
        errors.AddRange(await ValidateChannelsAsync(channelIds, ct));

        if (errors.Count > 0)
        {
            return ParameterSaveResult.Failed(errors);
        }

        var now = _time.GetUtcNow();
        var parameter = new Parameter
        {
            Id = Guid.NewGuid(),
            NameEn = command.NameEn!.Trim(),
            NameAr = command.NameAr!.Trim(),
            ApiField = command.ApiField!.Trim(),
            // BR-11: a brand-new field name has no traffic behind it, so it stays renameable until the first
            // request carries it. Only the seeded built-ins ship locked.
            ApiFieldLocked = false,
            DataType = command.DataType,
            RangeMin = command.DataType == DataType.Range ? command.RangeMin : null,
            RangeMax = command.DataType == DataType.Range ? command.RangeMax : null,
            RangeUnit = command.DataType == DataType.Range ? Normalise(command.RangeUnit) : null,
            ValidationRule = Normalise(command.ValidationRule),
            Origin = ParameterOrigin.Custom,
            Enabled = command.Enabled,
            RequiredByDefault = command.RequiredByDefault,
            Filterable = command.Filterable,
            ReportingVisibility = command.ReportingVisibility,
            DashboardVisibility = command.DashboardVisibility,
            // BR-27 is resolved server-side from the type — the submitted flag is a request, not the value.
            MappingSupport = MappingSupportPolicy.Resolve(command.DataType, command.MappingSupport),
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _db.ExecuteAsync(async () =>
        {
            _db.Parameters.Add(parameter);
            AssignChannels(parameter, channelIds);
            Audit("parameter.created", parameter, command.ActorId, command.ActorPersona, now,
                oldValue: null, newValue: Snapshot(parameter));

            await Task.CompletedTask;
        }, ct);

        await _dataScope.PublishAsync(ct);

        return ParameterSaveResult.Ok(await ProjectAsync(parameter.Id, ct)
            ?? throw new InvalidOperationException("The created parameter could not be read back."));
    }

    /// <inheritdoc />
    public async Task<ParameterSaveResult> PatchAsync(
        Guid id,
        ParameterPatchCommand command,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var parameter = await _db.Parameters.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (parameter is null)
        {
            return ParameterSaveResult.Failed(
                ParameterErrorCodes.ParameterNotFound, "Parameter not found");
        }

        var builtIn = parameter.Origin == ParameterOrigin.BuiltIn;
        var errors = new List<ParameterValidationError>();

        // BR-09 / [PO-G27] — consulted ONLY for changes the client actually asked for. A PATCH that omits
        // api_field or data_type is not a rename or a retype, so a built-in's read-only form still saves.
        var requestedApiField = Normalise(command.ApiField);
        var wantsRename = requestedApiField is not null
            && !string.Equals(requestedApiField, parameter.ApiField, StringComparison.Ordinal);
        var wantsRetype = command.DataType is not null && command.DataType != parameter.DataType;

        if (wantsRename)
        {
            _builtInGuard.Guard(builtIn, ParameterAction.RenameApiField);
        }

        if (wantsRetype)
        {
            _builtInGuard.Guard(builtIn, ParameterAction.ChangeDataType);
        }

        var dataType = command.DataType ?? parameter.DataType;

        if (!Enum.IsDefined(dataType))
        {
            return ParameterSaveResult.Failed(
                ParameterErrorCodes.InvalidDataType,
                "Select one of the supported parameter types",
                ParameterFields.DataType);
        }

        // BR-11 — the lock probe is live as well as persisted, so a parameter with traffic but an unwritten flag
        // is still treated as locked.
        errors.AddRange(_lockGuard
            .ValidateApiFieldChange(parameter, await HasReceivedRequestAsync(parameter.ApiField, ct), requestedApiField)
            .Errors);

        var nameEn = command.NameEn ?? parameter.NameEn;
        var nameAr = command.NameAr ?? parameter.NameAr;
        errors.AddRange(_nameValidator.Validate(nameEn, nameAr).Errors);

        if (wantsRename)
        {
            var otherApiFields = await _db.Parameters
                .AsNoTracking()
                .Where(p => p.Id != id)
                .Select(p => p.ApiField)
                .ToListAsync(ct);

            errors.AddRange(_apiFieldUniqueness.Validate(otherApiFields, requestedApiField).Errors);
        }

        // A type switch that leaves the Range card populated (or clears it on a Range) is caught here rather than
        // by the column CHECK — the submitted values are merged onto the stored ones first so an omitted field
        // keeps its value.
        var rangeMin = dataType == DataType.Range ? command.RangeMin ?? parameter.RangeMin : command.RangeMin;
        var rangeMax = dataType == DataType.Range ? command.RangeMax ?? parameter.RangeMax : command.RangeMax;
        var rangeUnit = dataType == DataType.Range ? command.RangeUnit ?? parameter.RangeUnit : command.RangeUnit;
        errors.AddRange(_rangeValidator.Validate(dataType, rangeMin, rangeMax, rangeUnit).Errors);

        var channelIds = command.ChannelIds is null ? null : Distinct(command.ChannelIds);
        if (channelIds is not null)
        {
            errors.AddRange(await ValidateChannelsAsync(channelIds, ct));
        }

        if (errors.Count > 0)
        {
            return ParameterSaveResult.Failed(errors);
        }

        // BR-10 — the impact scan runs only on a genuine enable → disable transition, and BEFORE anything is
        // written. Withholding the change (rather than rejecting it) is what makes D-6 a warning rather than a
        // block: the console shows the list, the user confirms, the same request is re-sent with the flag set.
        var disabling = command.Enabled == false && parameter.Enabled;
        IReadOnlyList<ParameterReference> references = Array.Empty<ParameterReference>();

        if (disabling)
        {
            references = await ScanReferencesAsync(parameter, ct);

            if (references.Count > 0 && !command.ConfirmDisable)
            {
                return ParameterSaveResult.ConfirmationRequired(
                    await ProjectAsync(id, ct) ?? throw new InvalidOperationException("Parameter vanished mid-request."),
                    references);
            }
        }

        var now = _time.GetUtcNow();
        var before = Snapshot(parameter);
        var wasEnabled = parameter.Enabled;

        await _db.ExecuteAsync(async () =>
        {
            parameter.NameEn = nameEn!.Trim();
            parameter.NameAr = nameAr!.Trim();
            parameter.DataType = dataType;
            parameter.RangeMin = dataType == DataType.Range ? rangeMin : null;
            parameter.RangeMax = dataType == DataType.Range ? rangeMax : null;
            parameter.RangeUnit = dataType == DataType.Range ? Normalise(rangeUnit) : null;
            parameter.ValidationRule = command.ValidationRule is null
                ? parameter.ValidationRule
                : Normalise(command.ValidationRule);
            parameter.Enabled = command.Enabled ?? parameter.Enabled;
            parameter.RequiredByDefault = command.RequiredByDefault ?? parameter.RequiredByDefault;
            parameter.Filterable = command.Filterable ?? parameter.Filterable;
            parameter.ReportingVisibility = command.ReportingVisibility ?? parameter.ReportingVisibility;
            parameter.DashboardVisibility = command.DashboardVisibility ?? parameter.DashboardVisibility;
            parameter.MappingSupport = MappingSupportPolicy.Resolve(
                dataType, command.MappingSupport ?? parameter.MappingSupport);
            parameter.UpdatedAt = now;

            if (wantsRename)
            {
                parameter.ApiField = requestedApiField!;
            }

            if (channelIds is not null)
            {
                // The submitted set is authoritative and replaces this parameter's assignments wholesale — an
                // omitted channel means "no longer assigned", which is how SCR-06's pills express removal.
                var stored = await _db.ChannelParameterAssignments
                    .Where(a => a.ParameterId == id)
                    .ToListAsync(ct);
                _db.ChannelParameterAssignments.RemoveRange(stored);
                AssignChannels(parameter, channelIds);
            }

            Audit("parameter.updated", parameter, command.ActorId, command.ActorPersona, now,
                before, Snapshot(parameter));

            // The transition-specific event contracts/api-endpoints.md requires alongside the generic update, so
            // an auditor can find an enable/disable without diffing payloads.
            if (wasEnabled != parameter.Enabled)
            {
                Audit(parameter.Enabled ? "parameter.enabled" : "parameter.disabled",
                    parameter, command.ActorId, command.ActorPersona, now,
                    JsonSerializer.Serialize(new { enabled = wasEnabled }),
                    JsonSerializer.Serialize(new { enabled = parameter.Enabled }));
            }
        }, ct);

        await _dataScope.PublishAsync(ct);

        var projection = await ProjectAsync(id, ct)
            ?? throw new InvalidOperationException("The updated parameter could not be read back.");

        return references.Count > 0
            ? ParameterSaveResult.Applied(projection, references)
            : ParameterSaveResult.Ok(projection);
    }

    /// <inheritdoc />
    public async Task<ParameterPage> ListAsync(
        ParameterListFilter? filter = null,
        string? cursor = null,
        int limit = DefaultPageSize,
        CancellationToken ct = default)
    {
        filter ??= ParameterListFilter.None;
        var pageSize = limit is < 1 or > MaxPageSize ? DefaultPageSize : limit;

        // The origin-tab counts are GLOBAL — computed before the filter is applied (AC-S5-01: "the tab counts stay
        // global, unaffected by the type filter"). A count that moved with the filter would contradict the result
        // list rather than navigate to it.
        var origins = await _db.Parameters
            .AsNoTracking()
            .GroupBy(p => p.Origin)
            .Select(g => new { Origin = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var builtInCount = origins.FirstOrDefault(o => o.Origin == ParameterOrigin.BuiltIn)?.Count ?? 0;
        var customCount = origins.FirstOrDefault(o => o.Origin == ParameterOrigin.Custom)?.Count ?? 0;
        var counts = new ParameterOriginCounts(builtInCount + customCount, builtInCount, customCount);

        var query = _db.Parameters.AsNoTracking().AsQueryable();

        // FR-S5-01 — the three filters AND-combine.
        if (filter.Origin is not null)
        {
            query = query.Where(p => p.Origin == filter.Origin);
        }

        if (filter.DataType is not null)
        {
            query = query.Where(p => p.DataType == filter.DataType);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLowerInvariant();
            query = query.Where(p =>
                p.NameEn.ToLower().Contains(term)
                || p.NameAr.Contains(term)
                || p.ApiField.Contains(term));
        }

        // Ordered by API field, then id, so the order is stable across pages. Materialising the filtered set first
        // is safe: VR-F13 caps a tenant at 200 customs + 23 built-ins, and no parameter is ever deleted (BR-09),
        // so the cursor row cannot vanish mid-pagination.
        var ordered = await query
            .OrderBy(p => p.ApiField)
            .ThenBy(p => p.Id)
            .ToListAsync(ct);

        var start = 0;
        if (!string.IsNullOrEmpty(cursor) && Guid.TryParseExact(cursor, "N", out var afterId))
        {
            var index = ordered.FindIndex(p => p.Id == afterId);
            start = index < 0 ? 0 : index + 1;
        }

        var page = ordered.Skip(start).Take(pageSize).ToList();
        var nextCursor = start + page.Count < ordered.Count && page.Count > 0
            ? page[^1].Id.ToString("N")
            : null;

        var ids = page.Select(p => p.Id).ToList();

        var mappingCounts = await _db.ParameterMappings
            .AsNoTracking()
            .Where(m => ids.Contains(m.ParameterId))
            .GroupBy(m => m.ParameterId)
            .Select(g => new { ParameterId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var assignments = await _db.ChannelParameterAssignments
            .AsNoTracking()
            .Where(a => ids.Contains(a.ParameterId))
            .Select(a => new { a.ParameterId, a.ServiceChannelId })
            .ToListAsync(ct);

        var items = page
            .Select(parameter => Map(
                parameter,
                mappingCounts.FirstOrDefault(m => m.ParameterId == parameter.Id)?.Count ?? 0,
                assignments.Where(a => a.ParameterId == parameter.Id).Select(a => a.ServiceChannelId).ToList()))
            .ToList();

        return new ParameterPage(items, nextCursor, counts);
    }

    /// <inheritdoc />
    public Task<ParameterDto?> GetAsync(Guid id, CancellationToken ct = default) => ProjectAsync(id, ct);

    // ── internals ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Assembles BR-10's reference list from all three consumer families: M-13's own channel contracts (read
    /// directly) plus M-10 scope filters and M-14/15/16 rules (asked for through
    /// <see cref="IExternalParameterReferenceReader"/>, which has no real provider yet — TODO-M13-005).
    /// </summary>
    private async Task<IReadOnlyList<ParameterReference>> ScanReferencesAsync(Parameter parameter, CancellationToken ct)
    {
        var channelNames = await _db.ChannelParameterAssignments
            .AsNoTracking()
            .Where(a => a.ParameterId == parameter.Id)
            .Join(
                _db.ServiceChannels.AsNoTracking(),
                a => a.ServiceChannelId,
                c => c.Id,
                (a, c) => c.NameEn)
            .ToListAsync(ct);

        var scopeFilters = await _externalReferences.GetDataScopeFilterNamesAsync(parameter.ApiField, ct);
        var rules = await _externalReferences.GetRuleNamesAsync(parameter.ApiField, ct);

        return _impactScanner.Scan(
            parameter.Id,
            channelNames.Select(name => new ParameterReferenceSource(parameter.Id, name)),
            scopeFilters.Select(name => new ParameterReferenceSource(parameter.Id, name)),
            rules.Select(name => new ParameterReferenceSource(parameter.Id, name)));
    }

    /// <summary>
    /// BR-11's live lock probe: has any logged request carried this API field? Complements the persisted
    /// <see cref="Parameter.ApiFieldLocked"/> flag (which US4's pipeline sets) so a parameter with traffic but an
    /// unwritten flag is still treated as locked. <c>parameters_received</c> is <c>jsonb</c>, so this is a
    /// server-side key-existence test, not a scan of the payloads into memory.
    /// </summary>
    private Task<bool> HasReceivedRequestAsync(string apiField, CancellationToken ct) =>
        _db.IntegrationRequestLogs
            .AsNoTracking()
            .AnyAsync(log => EF.Functions.JsonExists(log.ParametersReceived, apiField), ct);

    /// <summary>Rejects channel assignments pointing at service channels that do not exist.</summary>
    private async Task<IReadOnlyList<ParameterValidationError>> ValidateChannelsAsync(
        IReadOnlyList<Guid> channelIds,
        CancellationToken ct)
    {
        if (channelIds.Count == 0)
        {
            return Array.Empty<ParameterValidationError>();
        }

        var known = await _db.ServiceChannels
            .AsNoTracking()
            .Where(c => channelIds.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync(ct);

        return channelIds
            .Except(known)
            .Select(unknown => new ParameterValidationError(
                ParameterErrorCodes.UnknownChannel,
                $"Service channel {unknown} does not exist",
                ParameterFields.ChannelIds))
            .ToList();
    }

    /// <summary>
    /// FR-S6-05 — a channel pill adds the parameter as <b>supported</b>, with the parameter's required-default
    /// applied. The channel's own contract screen (SCR-04) fine-tunes required/optional afterwards; BR-08 keeps
    /// that contract row authoritative at request time, not this seeded default.
    /// </summary>
    private void AssignChannels(Parameter parameter, IReadOnlyList<Guid> channelIds)
    {
        foreach (var channelId in channelIds)
        {
            _db.ChannelParameterAssignments.Add(new ChannelParameterAssignment
            {
                ServiceChannelId = channelId,
                ParameterId = parameter.Id,
                Supported = true,
                Required = parameter.RequiredByDefault,
            });
        }
    }

    private async Task<ParameterDto?> ProjectAsync(Guid id, CancellationToken ct)
    {
        var parameter = await _db.Parameters.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);

        if (parameter is null)
        {
            return null;
        }

        var mappings = await _db.ParameterMappings.AsNoTracking().CountAsync(m => m.ParameterId == id, ct);
        var channelIds = await _db.ChannelParameterAssignments
            .AsNoTracking()
            .Where(a => a.ParameterId == id)
            .Select(a => a.ServiceChannelId)
            .ToListAsync(ct);

        return Map(parameter, mappings, channelIds);
    }

    private static ParameterDto Map(Parameter parameter, int mappingsCount, IReadOnlyList<Guid> channelIds) =>
        new(
            parameter.Id,
            parameter.NameEn,
            parameter.NameAr,
            parameter.ApiField,
            // The wire flag ORs in the origin so a built-in always renders read-only, matching ApiFieldNameLockGuard.
            parameter.ApiFieldLocked || parameter.Origin == ParameterOrigin.BuiltIn,
            parameter.DataType,
            parameter.DataTypeLocked,
            parameter.RangeMin,
            parameter.RangeMax,
            parameter.RangeUnit,
            parameter.ValidationRule,
            parameter.Origin,
            parameter.Enabled,
            parameter.RequiredByDefault,
            parameter.Filterable,
            parameter.ReportingVisibility,
            parameter.DashboardVisibility,
            parameter.MappingSupport,
            MappingSupportPolicy.IsChangeable(parameter.DataType),
            mappingsCount,
            channelIds,
            parameter.CreatedAt,
            parameter.UpdatedAt);

    /// <summary>
    /// Appends the M-17 audit row for a parameter change. Tracked on the <b>same</b> context as the change, so the
    /// enclosing <c>ExecuteAsync</c> commits both together or neither (DB-08).
    ///
    /// <para>TODO(US9): T145's <c>AuditEventEmitter</c> takes this over and adds the correlation id — the column is
    /// nullable, and no correlation source is wired into this module yet.</para>
    /// </summary>
    private void Audit(
        string eventType,
        Parameter parameter,
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
            EntityType = nameof(Parameter),
            EntityId = parameter.Id,
            OldValue = oldValue,
            NewValue = newValue,
            OccurredAtUtc = occurredAt,
            CorrelationId = null,
        });

    private static string Snapshot(Parameter parameter) => JsonSerializer.Serialize(new
    {
        name_en = parameter.NameEn,
        name_ar = parameter.NameAr,
        api_field = parameter.ApiField,
        data_type = parameter.DataType.ToString(),
        range_min = parameter.RangeMin,
        range_max = parameter.RangeMax,
        range_unit = parameter.RangeUnit,
        validation_rule = parameter.ValidationRule,
        origin = parameter.Origin.ToString(),
        enabled = parameter.Enabled,
        required_by_default = parameter.RequiredByDefault,
        filterable = parameter.Filterable,
        reporting_visibility = parameter.ReportingVisibility,
        dashboard_visibility = parameter.DashboardVisibility,
        mapping_support = parameter.MappingSupport,
    });

    private static IReadOnlyList<Guid> Distinct(IReadOnlyList<Guid>? ids) =>
        ids is null ? Array.Empty<Guid>() : ids.Where(id => id != Guid.Empty).Distinct().ToList();

    private static string? Normalise(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
