using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Nabadat.IntegrationHub.Application.Parameters;
using Nabadat.IntegrationHub.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.IntegrationHub.IntegrationTests.Endpoints;

/// <summary>
/// T066 [US2] — end-to-end proof of M-13's <b>real</b> cross-module call to M-10 (research.md §4.1, CMC-06,
/// BR-10's forward half): creating or changing a mapping-enabled parameter pushes its name, label, and known
/// value set to <c>POST /api/v1/authorization/scope/parameters</c>, where
/// <c>Nabadat.UserManagement</c>'s <c>M13ParameterContractAdapter</c> validates and upserts it into
/// <c>data_scope_parameter_definitions</c>.
///
/// <para><b>This is not a mock assertion.</b> The fixture points M-13's outbound <c>HttpClient</c> at the same
/// in-memory test server, so the request traverses M-10's real controller, its real validator (reserved names,
/// empty value sets, the 500-per-payload ceiling) and its real persistence. The assertions read the resulting
/// rows out of the shared Testcontainers Postgres — which is what makes them evidence that the contract holds,
/// rather than evidence that M-13 called <i>something</i>.</para>
///
/// <para><b>Shared-fixture note:</b> the publisher sends the tenant's <i>full</i> qualifying set on every push
/// (M-10 upserts by name), so definitions left behind by earlier tests in the run are re-sent harmlessly. Each
/// test therefore asserts on its own parameter's row rather than on the total row count.</para>
/// </summary>
[Collection(IntegrationHubIntegrationCollection.Name)]
public sealed class DataScopeContractPublisherTests
{
    private const string Route = "/api/v1/integration-hub/parameters";

    private readonly IntegrationHubApplicationFactory _factory;

    public DataScopeContractPublisherTests(IntegrationHubApplicationFactory factory) => _factory = factory;

    private static string UniqueApiField(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..24];

    [Fact]
    public async Task Enabling_mappings_on_a_list_parameter_pushes_its_value_set_to_M10s_real_endpoint()
    {
        // Arrange: a List parameter whose mapping table gives it an enumerable value set. Mappings are US6's
        // story, so they are seeded directly — the push rule under test is US2's.
        var client = await _factory.SignedInClientAsync("P-01");
        var apiField = UniqueApiField("branch_group");
        var parameterId = await _factory.SeedCustomParameterAsync(
            nameEn: "Branch Group",
            nameAr: "مجموعة الفروع",
            apiField: apiField,
            dataType: "list",
            mappingSupport: true);

        await _factory.SeedParameterMappingAsync(parameterId, "BG-01", "Northern Group");
        await _factory.SeedParameterMappingAsync(parameterId, "BG-02", "Southern Group");

        // Act: any parameter change triggers the push. A label edit is the smallest one that proves the trigger
        // is the write path itself, not something specific to create.
        var response = await client.PatchAsJsonAsync(
            $"{Route}/{parameterId}", new { name_en = "Branch Grouping" });
        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);

        // Assert: M-10 holds the definition, with M-13 recorded as the source module.
        var definition = await _factory.GetDataScopeDefinitionAsync(apiField);

        definition.Should().NotBeNull("M-13's push must reach M-10's real endpoint and persist");
        definition!.Value.Label.Should().Be("Branch Grouping");
        definition.Value.AllowedValues.Should().BeEquivalentTo("BG-01", "BG-02");
        definition.Value.SourceModule.Should().Be("M-13");
    }

    [Fact]
    public async Task Creating_a_mapping_enabled_parameter_pushes_it_once_it_has_a_value_set()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var apiField = UniqueApiField("visit_reason");

        var created = await client.PostAsJsonAsync(Route, new
        {
            name_en = "Visit Reason",
            name_ar = "سبب الزيارة",
            api_field = apiField,
            data_type = "list",
        });
        await created.ShouldHaveStatusAsync(HttpStatusCode.Created);
        var parameterId = (await created.ReadJsonAsync()).GetProperty("id").GetGuid();

        // At create time the parameter has no mappings, so M-10 must NOT have received it — a definition with an
        // empty allowedValues set is rejected by M-10's validator, and rejection fails the WHOLE batch. Skipping
        // it locally is the behaviour that keeps every other parameter's push working.
        (await _factory.GetDataScopeDefinitionAsync(apiField)).Should()
            .BeNull("a parameter with no mapped values has no enumerable value set to publish");

        // Once it has values, the next change publishes it.
        await _factory.SeedParameterMappingAsync(parameterId, "VR-01", "Account opening");
        var patched = await client.PatchAsJsonAsync($"{Route}/{parameterId}", new { filterable = true });
        await patched.ShouldHaveStatusAsync(HttpStatusCode.OK);

        var definition = await _factory.GetDataScopeDefinitionAsync(apiField);
        definition.Should().NotBeNull();
        definition!.Value.AllowedValues.Should().BeEquivalentTo("VR-01");
    }

    [Fact]
    public async Task A_parameter_whose_api_field_is_on_M10s_reserved_list_is_dropped_rather_than_failing_the_batch()
    {
        // research.md §4.1's reconciliation task. M-10 refuses "persona" (it would shadow a real column when a
        // scope filter is applied) and fails the ENTIRE payload on one bad name. Filtering it out locally is what
        // stops one badly-named tenant parameter from silently stranding every other parameter's value set — so
        // the assertion is two-sided: the reserved one is absent, the innocent one still arrives.
        var client = await _factory.SignedInClientAsync("P-01");

        var reservedId = await _factory.SeedCustomParameterAsync(
            nameEn: "Persona", nameAr: "الشخصية", apiField: "persona", dataType: "list", mappingSupport: true);
        await _factory.SeedParameterMappingAsync(reservedId, "P-01", "CX Manager");

        var innocentField = UniqueApiField("survey_locale");
        var innocentId = await _factory.SeedCustomParameterAsync(
            nameEn: "Survey Locale", nameAr: "لغة الاستبيان", apiField: innocentField,
            dataType: "list", mappingSupport: true);
        await _factory.SeedParameterMappingAsync(innocentId, "ar-SA", "العربية");

        var response = await client.PatchAsJsonAsync($"{Route}/{innocentId}", new { filterable = true });
        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);

        (await _factory.GetDataScopeDefinitionAsync("persona")).Should()
            .BeNull("M-10 reserves 'persona'; pushing it would fail the whole batch");
        (await _factory.GetDataScopeDefinitionAsync(innocentField)).Should()
            .NotBeNull("the rest of the batch must survive a reserved name being filtered out");
    }

    [Fact]
    public async Task A_disabled_parameter_is_withdrawn_from_the_push()
    {
        // A disabled parameter should stop being offered as an M-10 filter dimension. M-10's endpoint only
        // upserts (it has no delete), so the observable behaviour is that the NEXT push no longer carries it —
        // the previously-stored row is stale until M-10 grows a removal path. The assertion pins what M-13
        // controls: its own outbound set.
        var client = await _factory.SignedInClientAsync("P-01");
        var apiField = UniqueApiField("closure_reason");
        var parameterId = await _factory.SeedCustomParameterAsync(
            nameEn: "Closure Reason", nameAr: "سبب الإغلاق", apiField: apiField,
            dataType: "list", mappingSupport: true);
        await _factory.SeedParameterMappingAsync(parameterId, "CR-01", "Resolved");

        // First push: enabled, so it lands in M-10.
        await (await client.PatchAsJsonAsync($"{Route}/{parameterId}", new { filterable = true }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
        (await _factory.GetDataScopeDefinitionAsync(apiField)).Should().NotBeNull();

        // Disable it, then verify the publisher no longer selects it.
        await (await client.PatchAsJsonAsync($"{Route}/{parameterId}", new { enabled = false }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<DataScopeContractPublisher>();
        var definitions = await publisher.BuildAsync();

        definitions.Should().NotContain(d => d.Name == apiField);
    }
}
