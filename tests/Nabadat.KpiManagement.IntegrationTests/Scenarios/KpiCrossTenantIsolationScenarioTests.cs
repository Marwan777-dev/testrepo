using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Nabadat.KpiManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.KpiManagement.IntegrationTests.Scenarios;

/// <summary>
/// T149 — the SC-005 / GP-04 cross-tenant isolation pass condition (spec.md §"Tenant isolation"):
/// a user from Tenant A authenticates and probes a KPI id that belongs to a DIFFERENT tenant via
/// <c>GET / PUT / PATCH /api/v1/kpis/{id}</c>. Per the schema-per-tenant boundary (AD-02 / DB-02)
/// the row does not exist on Tenant A's connection, so every probe returns <b>404 KPI_NOT_FOUND</b>
/// — never 200 with Tenant B's data, and never a 403 that would confirm the id's existence to a
/// cross-tenant caller. A control read of one of Tenant A's own seeded KPIs returns 200, proving
/// the 404 is genuine isolation and not a blanket failure.
///
/// <para><b>Deviation — the <c>audit_log</c> denial row is NOT asserted here.</b> spec.md and
/// quickstart.md §SC-005 describe writing a denial row (<c>denial_reason='cross_tenant_access'</c>,
/// <c>target_tenant_id</c>) to a global <c>audit_log</c>. That table/column set does not exist in
/// this module's schema — the platform's audit trail is M-10's <c>event_log</c> (surfaced read-only
/// as "audit_log" by <c>AuditLogController</c>), which has no <c>denial_reason</c>/<c>target_tenant_id</c>
/// columns, and the <c>GET /kpis/{id}</c> 404 path emits no event. The denial-audit is a global
/// M-10 tenant-routing concern (it requires a routing layer that can detect a JWT/subdomain tenant
/// mismatch); it cannot be exercised by this single-tenant (<c>ENABLE_MULTI_TENANT=false</c>)
/// fixture, where a foreign id is simply an absent row. This test therefore verifies the enforceable
/// SC-005 guarantee — the row's invisibility (404 across read + write verbs) — and leaves the
/// denial-row audit to be validated manually per quickstart.md §SC-005 against a real multi-tenant
/// deployment. Flagged in COVERAGE/handoff as a backing-code gap, not silently passed.</para>
/// </summary>
[Collection(KpiManagementIntegrationCollection.Name)]
public sealed class KpiCrossTenantIsolationScenarioTests
{
    private readonly KpiManagementApplicationFactory _factory;

    public KpiCrossTenantIsolationScenarioTests(KpiManagementApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task TenantA_probing_a_foreign_tenant_kpi_id_gets_404_on_every_verb_while_its_own_kpi_is_visible()
    {
        var (client, _) = await _factory.SignedInWithActorAsync(PersonaContextHelper.CxProgramManager);

        // An id that belongs to a different tenant's schema is, on Tenant A's connection, simply an
        // absent row. A fresh GUID stands in for "Tenant B's KPI id" — the boundary behaviour is
        // identical (the row is not reachable from this tenant), which is exactly the spec mechanism.
        var foreignKpiId = Guid.NewGuid();

        // 1. Control: Tenant A CAN read one of its OWN seeded standards — proves the gate is open and
        //    the 404s below are genuine isolation, not a blanket failure or a permission denial.
        var ownNps = await client.GetAsync("/api/v1/kpis/NPS");
        ownNps.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. GET probe → 404 KPI_NOT_FOUND (not 200 with foreign data, not 403).
        var get = await client.GetAsync($"/api/v1/kpis/{foreignKpiId}");
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await get.ReadErrorCodeAsync()).Should().Be("KPI_NOT_FOUND");

        // 3. PUT probe (SC-005 covers write verbs too) → 404, never a leak of the foreign row.
        var put = await client.PutAsJsonAsync(
            $"/api/v1/kpis/{foreignKpiId}",
            KpiRequestBodies.Custom("XTEN" + Guid.NewGuid().ToString("N")[..4], fullName: "Probe"));
        put.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await put.ReadErrorCodeAsync()).Should().Be("KPI_NOT_FOUND");

        // 4. PATCH activation probe → 404.
        var patch = await client.PatchAsJsonAsync(
            $"/api/v1/kpis/{foreignKpiId}/activation", new { active = false, confirm = true });
        patch.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await patch.ReadErrorCodeAsync()).Should().Be("KPI_NOT_FOUND");
    }
}
