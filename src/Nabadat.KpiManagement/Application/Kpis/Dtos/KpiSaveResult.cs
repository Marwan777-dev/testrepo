using Nabadat.KpiManagement.Application.Kpis.Services;
namespace Nabadat.KpiManagement.Application.Kpis.Dtos;

/// <summary>
/// Outcome of <see cref="KpiSaveService.SaveAsync"/>. On success <see cref="Succeeded"/> is true and
/// <see cref="KpiId"/> identifies the saved KPI; on failure <see cref="ErrorCode"/> carries the
/// stable validation / business-rule code the API layer maps to the API-05 envelope.
/// </summary>
/// <param name="Succeeded">True when the KPI (and its threshold, perspectives, audit row) committed.</param>
/// <param name="KpiId">The saved KPI's id.</param>
/// <param name="ErrorCode">The failure code when <see cref="Succeeded"/> is false; null on success.</param>
public sealed record KpiSaveResult(bool Succeeded, Guid KpiId, string? ErrorCode);
