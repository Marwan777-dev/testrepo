using System.Net.Http.Json;
using FluentAssertions;
using Nabadat.KpiManagement.IntegrationTests.Infrastructure;
using Npgsql;
using Xunit;

namespace Nabadat.KpiManagement.IntegrationTests.Services;

/// <summary>
/// Atomicity coverage for the KPI create write (DB-08 — the definition + threshold + perspectives +
/// the M-17 audit row commit or roll back together inside one transaction). We induce a failure
/// mid-transaction by submitting a perspective label longer than the <c>varchar(60)</c> column: the
/// definition and threshold flush first, then the perspective insert fails with PostgreSQL 22001,
/// rolling the whole transaction back. The assertion is that NOTHING survives — no definition row, no
/// perspective row, and no <c>settings.changed</c> event.
/// </summary>
[Collection(KpiManagementIntegrationCollection.Name)]
public sealed class KpiSaveAtomicityTests
{
    private readonly KpiManagementApplicationFactory _factory;

    public KpiSaveAtomicityTests(KpiManagementApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task POST_kpis_rolls_back_definition_threshold_and_event_when_a_perspective_insert_fails()
    {
        var (client, actor) = await _factory.SignedInWithActorAsync(PersonaContextHelper.CxProgramManager);
        var shortName = "ATOM" + Guid.NewGuid().ToString("N")[..6];
        var overLongLabel = new string('x', 61); // exceeds kpi_perspectives.label varchar(60)

        var body = KpiRequestBodies.Custom(
            shortName,
            perspectives: new object[] { new { label = overLongLabel, display_order = 0 } });

        var response = await client.PostAsJsonAsync("/api/v1/kpis", body);

        // The mid-transaction failure surfaces as a non-2xx (no partial success).
        response.IsSuccessStatusCode.Should().BeFalse();

        // Nothing persisted: no definition row, no event, no perspective row carrying the label.
        (await _factory.GetKpiIdByShortNameAsync(shortName)).Should().BeNull();
        (await _factory.CountEventsAsync(actor.UserId, "settings.changed")).Should().Be(0);
        (await CountPerspectivesWithLabelAsync(overLongLabel)).Should().Be(0);
    }

    private async Task<int> CountPerspectivesWithLabelAsync(string label)
    {
        await using var connection = new NpgsqlConnection(_factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM kpi_perspectives WHERE label = @l", connection);
        command.Parameters.AddWithValue("l", label);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
}
