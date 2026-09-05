using Nabadat.KpiManagement.Application.Cxi.Interfaces;
using Nabadat.KpiManagement.Application.Events;
using Nabadat.KpiManagement.Application.Interfaces;
using Nabadat.KpiManagement.Application.Kpis.Interfaces;
using Nabadat.KpiManagement.Domain.Entities;

namespace Nabadat.KpiManagement.Application.Cxi;

/// <summary>
/// T087 [US-3] — the atomic full-replace orchestrator behind <c>PUT /api/v1/kpis/{cxi_id}/weights</c>
/// (contracts/kpi-api.md). Validates the requested weights against the CXI rules, then replaces the
/// <c>cxi_weights</c> rows and emits one <c>settings.changed</c> event inside a single
/// <see cref="ITenantDbContext.ExecuteAsync(Func{Task}, CancellationToken)"/> transaction, so the
/// weight rows and the audit row commit (or roll back) together (DB-08 — the context is the unit of
/// work). All validation runs before the transaction opens; a failure never touches the context.
/// <para>
/// Rules: zero-weight entries are silently dropped (BR-2.3); a remaining non-positive weight →
/// <see cref="WeightInvalidCode"/>; a member equal to the CXI itself → <see cref="CannotIncludeItselfCode"/>;
/// a member that is not an existing active KPI → <see cref="MemberNotActiveCode"/>; and (FR-043) fewer
/// than two non-zero weights while the CXI is active → <see cref="InsufficientMembersCode"/>.
/// </para>
/// </summary>
public sealed class CxiWeightUpdateService
{
    public const string CxiNotFoundCode = "CXI_NOT_FOUND";
    public const string CannotIncludeItselfCode = "CXI_CANNOT_INCLUDE_ITSELF";
    public const string MemberNotActiveCode = "CXI_MEMBER_NOT_ACTIVE";
    public const string InsufficientMembersCode = "CXI_INSUFFICIENT_MEMBERS";
    public const string WeightInvalidCode = "CXI_WEIGHT_INVALID";

    private readonly ITenantDbContext _context;
    private readonly IKpiDefinitionService _definitions;
    private readonly ICxiWeightService _cxiWeights;
    private readonly KpiEventPublisher _events;
    private readonly TimeProvider _timeProvider;

    public CxiWeightUpdateService(
        ITenantDbContext context,
        IKpiDefinitionService definitions,
        ICxiWeightService cxiWeights,
        KpiEventPublisher events,
        TimeProvider timeProvider)
    {
        _context = context;
        _definitions = definitions;
        _cxiWeights = cxiWeights;
        _events = events;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Full-replace the CXI's member weights. Returns a failure result (no writes, no event) when any
    /// rule is violated; otherwise commits the new rows + one audit event atomically.
    /// </summary>
    public async Task<CxiWeightUpdateResult> ReplaceAsync(
        Guid cxiId,
        IReadOnlyList<CxiWeightInput> weights,
        Guid actorId,
        string actorPersona,
        Guid correlationId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(weights);

        var cxi = await _definitions.GetByIdAsync(cxiId, ct);
        if (cxi is null || !cxi.IsComposite)
        {
            return CxiWeightUpdateResult.Fail(CxiNotFoundCode);
        }

        if (weights.Any(w => w.MemberKpiId == cxiId))
        {
            return CxiWeightUpdateResult.Fail(CannotIncludeItselfCode);
        }

        // BR-2.3: zero-weight entries are silently dropped; a remaining non-positive weight is invalid.
        var members = weights.Where(w => w.Weight != 0).ToList();
        if (members.Any(w => w.Weight <= 0))
        {
            return CxiWeightUpdateResult.Fail(WeightInvalidCode);
        }

        var byId = (await _definitions.ListAllAsync(ct)).ToDictionary(k => k.Id);
        if (members.Any(w => !byId.TryGetValue(w.MemberKpiId, out var member) || !member.IsActive))
        {
            return CxiWeightUpdateResult.Fail(MemberNotActiveCode);
        }

        // FR-043: an active CXI must retain at least two weighted members.
        if (cxi.IsActive && !CxiActivationRule.CanActivate(members.Select(w => w.Weight).ToList()))
        {
            return CxiWeightUpdateResult.Fail(InsufficientMembersCode);
        }

        var occurredAt = _timeProvider.GetUtcNow();
        var previous = await _cxiWeights.ListByCxiKpiIdAsync(cxiId, ct);

        var rows = members
            .Select(w => new CxiWeight
            {
                CxiKpiId = cxiId,
                MemberKpiId = w.MemberKpiId,
                Weight = (short)w.Weight,
                CreatedAt = occurredAt,
            })
            .ToList();

        await _context.ExecuteAsync(async () =>
        {
            await _cxiWeights.ReplaceAllAsync(cxiId, rows, ct);

            var oldValue = new
            {
                cxi_weights = previous
                    .Select(w => new { member_kpi_id = w.MemberKpiId, weight = (int)w.Weight })
                    .ToArray(),
            };
            var newValue = new
            {
                action = "updated",
                changes = new
                {
                    cxi_weights = members
                        .Select(w => new { member_kpi_id = w.MemberKpiId, weight = w.Weight })
                        .ToArray(),
                },
            };

            await _events.PublishKpiSettingsChangedAsync(
                cxiId, actorId, actorPersona, oldValue, newValue, occurredAt, correlationId, ct);
        }, ct);

        return CxiWeightUpdateResult.Ok();
    }
}
