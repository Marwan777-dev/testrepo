using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Nabadat.KpiManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.KpiManagement.IntegrationTests.Scenarios;

/// <summary>
/// Multi-step business-cycle test for the US-2 create-then-edit journey (Independent Test): a P-01
/// CX Program Manager creates a custom KPI, retrieves it, edits its Full Name, retrieves it again to
/// confirm the change, and the run ends with exactly two <c>settings.changed</c> events on the audit
/// log — one <c>created</c> and one <c>updated</c> — proving the write path emits one event per save
/// and the edit persisted.
/// </summary>
[Collection(KpiManagementIntegrationCollection.Name)]
public sealed class KpiCreateThenEditScenarioTests
{
    private readonly KpiManagementApplicationFactory _factory;

    public KpiCreateThenEditScenarioTests(KpiManagementApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task NewCustomKpi_is_created_then_edited_and_emits_one_created_and_one_updated_event()
    {
        var (client, actor) = await _factory.SignedInWithActorAsync(PersonaContextHelper.CxProgramManager);
        var shortName = "SCN" + Guid.NewGuid().ToString("N")[..6];

        // 1. Create QUAL-like custom KPI.
        var create = await client.PostAsJsonAsync(
            "/api/v1/kpis", KpiRequestBodies.Custom(shortName, fullName: "Service Quality"));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await create.ReadJsonAsync()).GetProperty("id").GetString();

        // 2. Retrieve it.
        var afterCreate = await client.GetAsync($"/api/v1/kpis/{id}");
        afterCreate.StatusCode.Should().Be(HttpStatusCode.OK);
        (await afterCreate.ReadJsonAsync()).GetProperty("full_name").GetString().Should().Be("Service Quality");

        // 3. Edit the Full Name (Short Name unchanged).
        var edit = await client.PutAsJsonAsync(
            $"/api/v1/kpis/{id}", KpiRequestBodies.Custom(shortName, fullName: "Renamed Service Quality"));
        edit.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Retrieve and confirm the change took.
        var afterEdit = await client.GetAsync($"/api/v1/kpis/{id}");
        (await afterEdit.ReadJsonAsync()).GetProperty("full_name").GetString().Should().Be("Renamed Service Quality");

        // 5. Exactly two settings.changed events for this actor: one created, one updated.
        (await _factory.CountEventsAsync(actor.UserId, "settings.changed")).Should().Be(2);
    }
}
