using Nabadat.KpiManagement.Domain.Entities;

namespace Nabadat.KpiManagement.Application.Catalogue.Dtos;

/// <summary>
/// One canonical standard-KPI seed: a <see cref="KpiDefinition"/> row bundled with its default
/// <see cref="KpiThreshold"/> band. <see cref="KpiSeedDataProvider"/> exposes the eight of these as
/// the single in-code source of canonical seed truth — mirrored by (and kept in step with)
/// <c>KpiManagement_Baseline.sql</c>, which performs the real per-tenant seed.
/// </summary>
public sealed record KpiSeed(KpiDefinition Definition, KpiThreshold Threshold);
