using Nabadat.KpiManagement.Domain.Entities;
using Nabadat.KpiManagement.Application.Kpis.Services;

namespace Nabadat.KpiManagement.Application.Kpis.Dtos;

/// <summary>
/// The unit of work for <see cref="KpiSaveService.SaveAsync"/>: the KPI definition, its threshold
/// band, and its full perspective set (full-replace, FR-028), plus the actor attribution carried
/// onto the M-17 audit event. The same shape serves create and edit (distinguished by
/// <paramref name="Mode"/>).
/// </summary>
/// <param name="Mode">Create (insert) or Edit (update).</param>
/// <param name="Definition">The KPI definition to persist (its <c>Id</c> identifies the row on edit).</param>
/// <param name="Threshold">The performance-band thresholds (its <c>KpiId</c> is bound to the definition).</param>
/// <param name="Perspectives">The complete perspective set — replaces any existing rows.</param>
/// <param name="ActorId">M-10 user id of the author/editor (audit attribution).</param>
/// <param name="ActorPersona">Actor persona <c>P-01</c>..<c>P-08</c>.</param>
/// <param name="CorrelationId">Per-request correlation id stamped on the audit event.</param>
public sealed record KpiSaveCommand(
    KpiSaveMode Mode,
    KpiDefinition Definition,
    KpiThreshold Threshold,
    IReadOnlyList<KpiPerspective> Perspectives,
    Guid ActorId,
    string ActorPersona,
    Guid CorrelationId);
