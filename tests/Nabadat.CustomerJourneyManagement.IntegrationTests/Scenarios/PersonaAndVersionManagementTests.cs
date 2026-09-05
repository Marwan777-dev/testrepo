using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.Events;
using Nabadat.CustomerJourneyManagement.Application.Personas;
using Nabadat.CustomerJourneyManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.CustomerJourneyManagement.IntegrationTests.Scenarios;

/// <summary>
/// US-3 business-cycle scenario (T076, <c>quickstart.md §3</c>): a P-01 author manages a persona
/// through its lifecycle, binds it to a journey, publishes an immutable journey version, and confirms
/// the version snapshot stays frozen while the live journey is edited. One test walks the whole flow
/// and asserts the final state-of-the-world (selector membership, frozen snapshot, the archive guard,
/// and the emitted audit trail), matching the spec's <c>Independent Test</c>:
/// <list type="number">
///   <item><description>create persona → status <c>Draft</c>;</description></item>
///   <item><description>Draft → Active → the persona appears in the binding selector
///   (<c>GET /api/v1/personas?status=Active</c>);</description></item>
///   <item><description>bind the persona to a journey → binding persisted;</description></item>
///   <item><description>Active → Inactive → the persona no longer appears in the binding selector;</description></item>
///   <item><description>publish the journey version → version 1 created with a snapshot;</description></item>
///   <item><description>edit the journey name → re-fetch version 1 → the snapshot is unchanged (immutable);</description></item>
///   <item><description>archive the persona while it still has an active binding → 409
///   <c>persona.archive_blocked_active_bindings</c>.</description></item>
/// </list>
/// The closing aggregate audit check (one <c>persona.created</c>, two <c>persona.status.changed</c>,
/// one <c>journey.version.published</c>) doubles as proof that the rejected archive emitted nothing.
///
/// One step from the spec narrative is intentionally split out below: <b>P-02 attempts to publish →
/// 403</b>. Persona authorization is not yet enforced in M-16 (deferred to the M-10 authorization
/// integration), so that case is present as a sibling <c>Skip</c>ped test, exactly as
/// <c>JourneyDefinitionFlowTests</c> handles the P-03→403 step. Persona→journey binding has no HTTP
/// endpoint (it is a <see cref="PersonaService"/> operation), so the bind step drives the service from
/// a DI scope — the established <c>KpiWeightEnforcementTests</c> pattern.
/// </summary>
[Collection(CustomerJourneyManagementIntegrationCollection.Name)]
public sealed class PersonaAndVersionManagementTests
{
    private readonly CustomerJourneyManagementApplicationFactory _factory;

    public PersonaAndVersionManagementTests(CustomerJourneyManagementApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task P01_manages_persona_lifecycle_binds_to_journey_and_publishes_an_immutable_version()
    {
        var (client, actor) = await _factory.SignedInWithActorAsync("P-01");

        // 1. Create the persona → starts in Draft.
        var create = await client.PostAsJsonAsync(
            "/api/v1/personas",
            new { nameAr = "العميل الرقمي", nameEn = $"Digital Customer {Guid.NewGuid():N}" });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var createBody = await create.ReadJsonAsync();
        createBody.GetProperty("status").GetString().Should().Be("Draft");
        var personaId = createBody.GetProperty("personaId").GetGuid();

        // A Draft persona is not yet bindable — it must not appear in the Active selector.
        (await BindableSelectorContainsAsync(client, personaId)).Should().BeFalse();

        // 2. Draft → Active → the persona now appears in the binding selector.
        (await client.PatchAsJsonAsync($"/api/v1/personas/{personaId}/status", new { status = "Active" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await BindableSelectorContainsAsync(client, personaId)).Should().BeTrue();

        // 3. Bind the (Active) persona to a journey → binding persisted (visible on the persona detail).
        var (journeyId, originalName) = await SeedMeasuredJourneyAsync(client);
        await BindPersonaToJourneyAsync(journeyId, personaId, actor);
        var bound = await (await client.GetAsync($"/api/v1/personas/{personaId}")).ReadJsonAsync();
        bound.GetProperty("journeyBindings").EnumerateArray()
            .Select(b => b.GetProperty("journeyId").GetGuid())
            .Should().Contain(journeyId);

        // 4. Active → Inactive → the persona drops out of the binding selector (the binding row remains).
        (await client.PatchAsJsonAsync($"/api/v1/personas/{personaId}/status", new { status = "Inactive" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await BindableSelectorContainsAsync(client, personaId)).Should().BeFalse();

        // 5. Publish the journey version → version 1 created with a self-contained snapshot.
        var publish = await client.PostAsync($"/api/v1/journeys/{journeyId}/publish", content: null);
        publish.StatusCode.Should().Be(HttpStatusCode.Created);
        (await publish.ReadJsonAsync()).GetProperty("versionNumber").GetInt32().Should().Be(1);

        // 6. Edit the journey name → the live journey changes, but version 1's snapshot stays frozen.
        var renamed = $"Renamed {Guid.NewGuid():N}";
        (await client.PutAsJsonAsync($"/api/v1/journeys/{journeyId}", new { name = renamed, journeyType = "Onboarding" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var liveName = (await (await client.GetAsync($"/api/v1/journeys/{journeyId}")).ReadJsonAsync())
            .GetProperty("name").GetString();
        liveName.Should().Be(renamed);

        var snapshot = await (await client.GetAsync($"/api/v1/journeys/{journeyId}/versions/1")).ReadJsonAsync();
        snapshot.GetProperty("isSnapshot").GetBoolean().Should().BeTrue();
        snapshot.GetProperty("name").GetString().Should().Be(originalName);

        // 7. Archive the persona while it still has an active binding → 409, and nothing changes.
        var archive = await client.PatchAsJsonAsync(
            $"/api/v1/personas/{personaId}/status", new { status = "Archived" });
        archive.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await archive.ReadErrorCodeAsync()).Should().Be("persona.archive_blocked_active_bindings");
        var afterArchive = await (await client.GetAsync($"/api/v1/personas/{personaId}")).ReadJsonAsync();
        afterArchive.GetProperty("status").GetString().Should().Be("Inactive");

        // 8. The flow emitted its audit trail in-band, and only for the writes that succeeded: one
        //    create, two status changes (→Active, →Inactive — the blocked archive emitted nothing),
        //    and one version publish.
        (await _factory.CountEventsAsync(actor.UserId, CustomerJourneyManagementEventTypes.PersonaCreated)).Should().Be(1);
        (await _factory.CountEventsAsync(actor.UserId, CustomerJourneyManagementEventTypes.PersonaStatusChanged)).Should().Be(2);
        (await _factory.CountEventsAsync(actor.UserId, CustomerJourneyManagementEventTypes.JourneyVersionPublished)).Should().Be(1);
    }

    [Fact(Skip = "Persona authorization (journey.publish → P-02 forbidden on publish) is not yet " +
                 "enforced in M-16: no AddAuthorization/UseAuthorization pipeline or persona check " +
                 "exists. Deferred to the M-10 authorization integration. Un-skip once journey.publish " +
                 "authorization lands.")]
    public async Task P02_is_forbidden_from_publishing_a_journey_version()
    {
        var p01 = await _factory.SignedInClientAsync("P-01");
        var (journeyId, _) = await SeedMeasuredJourneyAsync(p01);

        var p02 = await _factory.SignedInClientAsync("P-02");
        var response = await p02.PostAsync($"/api/v1/journeys/{journeyId}/publish", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// True when the persona appears in the journey-builder binding selector
    /// (<c>GET /api/v1/personas?status=Active</c>) — i.e. it is currently bindable.
    /// </summary>
    private static async Task<bool> BindableSelectorContainsAsync(HttpClient client, Guid personaId)
    {
        var response = await client.GetAsync("/api/v1/personas?status=Active");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        return body.GetProperty("items").EnumerateArray()
            .Any(p => p.GetProperty("personaId").GetGuid() == personaId);
    }

    /// <summary>
    /// Creates a journey → stage → touchpoint with a valid 100% KPI binding set, so the published
    /// snapshot has a non-trivial self-contained tree. Returns the journey id and its (original) name.
    /// </summary>
    private static async Task<(Guid JourneyId, string Name)> SeedMeasuredJourneyAsync(HttpClient client)
    {
        var name = $"Journey {Guid.NewGuid():N}";
        var create = await client.PostAsJsonAsync(
            "/api/v1/journeys", new { name, journeyType = "Onboarding" });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var journeyId = (await create.ReadJsonAsync()).GetProperty("journeyId").GetGuid();

        var stage = await client.PostAsJsonAsync($"/api/v1/journeys/{journeyId}/stages", new { name = "Awareness" });
        stage.StatusCode.Should().Be(HttpStatusCode.Created);
        var stageId = (await stage.ReadJsonAsync()).GetProperty("stageId").GetGuid();

        var touchpoint = await client.PostAsJsonAsync($"/api/v1/stages/{stageId}/touchpoints", new { name = "Landing page" });
        touchpoint.StatusCode.Should().Be(HttpStatusCode.Created);
        var touchpointId = (await touchpoint.ReadJsonAsync()).GetProperty("touchpointId").GetGuid();

        var kpis = await client.PutAsJsonAsync(
            $"/api/v1/touchpoints/{touchpointId}/kpis",
            new { kpiBindings = new[] { new { kpiType = "NPS", weight = 60 }, new { kpiType = "CSAT", weight = 40 } } });
        kpis.StatusCode.Should().Be(HttpStatusCode.OK);

        return (journeyId, name);
    }

    /// <summary>
    /// Binds an Active persona to a journey by driving <see cref="PersonaService"/> directly (resolved
    /// from a fresh DI scope, as the service is registered <c>Scoped</c>) — binding is a service-layer
    /// operation with no HTTP endpoint.
    /// </summary>
    private async Task BindPersonaToJourneyAsync(Guid journeyId, Guid personaId, SeededUser actor)
    {
        using var scope = _factory.Services.CreateScope();
        var personas = scope.ServiceProvider.GetRequiredService<PersonaService>();
        var actorContext = new ActorContext(actor.UserId, "P-01", Guid.NewGuid());

        var result = await personas.BindPersonaToJourneyAsync(journeyId, personaId, actorContext);
        result.IsSuccess.Should().BeTrue("an Active persona should bind to a journey");
    }
}
