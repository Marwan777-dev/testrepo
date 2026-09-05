using FluentAssertions;
using Nabadat.KpiManagement.IntegrationTests.Infrastructure;
using Npgsql;
using Xunit;
using Nabadat.KpiManagement.Application.Kpis.Services;

namespace Nabadat.KpiManagement.IntegrationTests.Services;

/// <summary>
/// Persistence-level coverage for BR-1.2 — Short Name uniqueness is enforced per tenant
/// (case-insensitive) across BOTH standard and custom KPIs. Enforcement lives in the
/// <c>kpi_definitions_short_name_lower_uniq</c> functional unique index (<c>LOWER(short_name)</c>),
/// so a colliding insert fails with PostgreSQL <c>23505</c> (unique_violation). There is no create
/// endpoint yet (US-2), so these tests exercise the invariant directly through the seeding helper,
/// which issues the same INSERT the eventual write path will.
/// </summary>
[Collection(KpiManagementIntegrationCollection.Name)]
public sealed class KpiShortNameUniquenessTests
{
    private readonly KpiManagementApplicationFactory _factory;

    public KpiShortNameUniquenessTests(KpiManagementApplicationFactory factory) => _factory = factory;

    [Fact] // BR-1.2 — collision against a seeded STANDARD KPI, case-insensitively.
    public async Task Inserting_a_kpi_whose_short_name_matches_a_standard_case_insensitively_is_rejected()
    {
        // "nps" differs only in case from the seeded standard "NPS".
        await FluentActions
            .Awaiting(() => _factory.SeedCustomKpiAsync("nps", "Duplicate of the NPS standard"))
            .Should().ThrowAsync<PostgresException>()
            .Where(e => e.SqlState == PostgresErrorCodes.UniqueViolation);
    }

    [Fact] // BR-1.2 — collision against another CUSTOM KPI, case-insensitively.
    public async Task Inserting_a_kpi_whose_short_name_matches_a_custom_case_insensitively_is_rejected()
    {
        // Lowercase-hex name; its upper-cased variant collides case-insensitively.
        var custom = "Dup" + Guid.NewGuid().ToString("N")[..8];
        await _factory.SeedCustomKpiAsync(custom, "Original custom KPI");

        await FluentActions
            .Awaiting(() => _factory.SeedCustomKpiAsync(custom.ToUpperInvariant(), "Case-variant duplicate"))
            .Should().ThrowAsync<PostgresException>()
            .Where(e => e.SqlState == PostgresErrorCodes.UniqueViolation);
    }

    [Fact(Skip =
        "BR-1.2 'trimmed' clause is NOT yet enforced: the LOWER(short_name) unique index does not " +
        "trim, and the write path (KpiDefinitionService.AddAsync) performs no normalization — the " +
        "create flow is US-2 (no POST endpoint exists). Un-skip once the create flow trims Short " +
        "Name before persistence so ' NPS ' collides with 'NPS'.")]
    public async Task Inserting_a_kpi_whose_short_name_matches_a_standard_after_trimming_is_rejected()
    {
        await FluentActions
            .Awaiting(() => _factory.SeedCustomKpiAsync(" NPS ", "Whitespace-padded duplicate of NPS"))
            .Should().ThrowAsync<PostgresException>()
            .Where(e => e.SqlState == PostgresErrorCodes.UniqueViolation);
    }
}
