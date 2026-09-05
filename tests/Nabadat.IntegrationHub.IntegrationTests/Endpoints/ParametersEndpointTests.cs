using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Nabadat.IntegrationHub.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.IntegrationHub.IntegrationTests.Endpoints;

/// <summary>
/// T065 [US2] — HTTP-level tests for the parameter-catalogue endpoints (SCR-05/06,
/// contracts/api-endpoints.md). Covers spec.md US2's Integration Test Coverage: create a custom Range parameter
/// → 201 + the <c>parameter.created</c> audit row; a duplicate API field (including against a <b>disabled</b> and
/// a <b>built-in</b> parameter) → 409; disable an unreferenced parameter → 200 with no warning; disable a
/// referenced one → 200 carrying BR-10's reference list and leaving the parameter unchanged until confirmed;
/// the AND-combined <c>origin</c> + <c>type</c> filter; and the absence of any <c>DELETE</c> endpoint (BR-09).
///
/// <para><b>Shared-fixture hygiene:</b> this lane writes real rows and never rolls back, so every parameter here
/// takes a unique API field — the <c>parameters_api_field_uniq</c> index would otherwise make tests collide with
/// each other rather than with their own arrangement. VR-F13 also caps a tenant at 200 custom parameters, so the
/// create-path tests depend on the shared container staying under that ceiling (TODO-M13-004).</para>
/// </summary>
[Collection(IntegrationHubIntegrationCollection.Name)]
public sealed class ParametersEndpointTests
{
    private const string Route = "/api/v1/integration-hub/parameters";

    private readonly IntegrationHubApplicationFactory _factory;

    public ParametersEndpointTests(IntegrationHubApplicationFactory factory) => _factory = factory;

    /// <summary>A unique <c>snake_case</c> field inside BR-11's <c>^[a-z][a-z0-9_]*$</c> format.</summary>
    private static string UniqueApiField(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..24];

    private static string ArabicName => "وقت الانتظار";

    [Fact]
    public async Task POST_parameters_returns_201_with_range_config_and_emits_parameter_created_when_input_is_valid()
    {
        var (client, actor) = await _factory.SignedInWithActorAsync("P-01");
        var apiField = UniqueApiField("wait_time");

        var response = await client.PostAsJsonAsync(Route, new
        {
            name_en = "Wait Time",
            name_ar = ArabicName,
            api_field = apiField,
            data_type = "range",
            range_min = 0,
            range_max = 120,
            range_unit = "minutes",
            dashboard_visibility = true,
        });

        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        var body = await response.ReadJsonAsync();
        body.GetProperty("api_field").GetString().Should().Be(apiField);
        body.GetProperty("data_type").GetString().Should().Be("range");
        body.GetProperty("origin").GetString().Should().Be("custom");
        body.GetProperty("range_min").GetDecimal().Should().Be(0m);
        body.GetProperty("range_max").GetDecimal().Should().Be(120m);
        body.GetProperty("range_unit").GetString().Should().Be("minutes");

        // A brand-new field has no traffic behind it, so BR-11's lock is still open; a custom parameter's type is
        // never locked ([PO-G27] applies to built-ins only).
        body.GetProperty("api_field_locked").GetBoolean().Should().BeFalse();
        body.GetProperty("data_type_locked").GetBoolean().Should().BeFalse();

        // BR-27: Range is outside {list, text, boolean, url}, so mapping support is forced off AND not offerable.
        body.GetProperty("mapping_support").GetBoolean().Should().BeFalse();
        body.GetProperty("mapping_support_changeable").GetBoolean().Should().BeFalse();

        var id = body.GetProperty("id").GetString();
        var get = await client.GetAsync($"{Route}/{id}");
        await get.ShouldHaveStatusAsync(HttpStatusCode.OK);

        (await _factory.CountEventsAsync(actor.UserId, "parameter.created")).Should().Be(1);
    }

    [Fact]
    public async Task POST_parameters_forces_mapping_support_on_for_a_list_type_even_when_the_client_sends_false()
    {
        // BR-27 / [PO-G25]: List is always mapping-enabled and not changeable, enforced SERVER-SIDE "even if a
        // client sends a contradicting value" (data-model.md §4). A contradicting request is corrected, not
        // rejected — rejecting it would make an honest client failure out of a rule the server owns.
        var client = await _factory.SignedInClientAsync("P-01");

        var response = await client.PostAsJsonAsync(Route, new
        {
            name_en = "Service Type",
            name_ar = "نوع الخدمة",
            api_field = UniqueApiField("service_type"),
            data_type = "list",
            mapping_support = false,
        });

        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        var body = await response.ReadJsonAsync();
        body.GetProperty("mapping_support").GetBoolean().Should().BeTrue();
        body.GetProperty("mapping_support_changeable").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task POST_parameters_honours_the_client_choice_of_mapping_support_for_a_text_type()
    {
        // The other BR-27 branch: text/boolean/url are user-changeable, default off.
        var client = await _factory.SignedInClientAsync("P-01");

        var response = await client.PostAsJsonAsync(Route, new
        {
            name_en = "Ticket Reference",
            name_ar = "مرجع التذكرة",
            api_field = UniqueApiField("ticket_ref"),
            data_type = "text",
            mapping_support = true,
        });

        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        var body = await response.ReadJsonAsync();
        body.GetProperty("mapping_support").GetBoolean().Should().BeTrue();
        body.GetProperty("mapping_support_changeable").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task POST_parameters_returns_409_duplicate_api_field_when_the_name_belongs_to_a_built_in()
    {
        // VR-F06 — built-ins share the field-name namespace from day one; "branch" is taken by the BR-23 seed.
        var client = await _factory.SignedInClientAsync("P-01");

        var response = await client.PostAsJsonAsync(Route, new
        {
            name_en = "Branch Code",
            name_ar = "رمز الفرع",
            api_field = "branch",
            data_type = "text",
        });

        await response.ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).Should().Be("validation.duplicate_api_field");
    }

    [Fact]
    public async Task POST_parameters_returns_409_duplicate_api_field_when_the_name_belongs_to_a_disabled_parameter()
    {
        // VR-F06's "including disabled" clause, and spec.md's Edge Case: disabling never frees the field name.
        var client = await _factory.SignedInClientAsync("P-01");
        var apiField = UniqueApiField("retired");
        await _factory.SeedCustomParameterAsync(
            nameEn: "Retired Field", apiField: apiField, dataType: "text", enabled: false);

        var response = await client.PostAsJsonAsync(Route, new
        {
            name_en = "Reused Field",
            name_ar = "حقل معاد",
            api_field = apiField,
            data_type = "text",
        });

        await response.ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).Should().Be("validation.duplicate_api_field");
    }

    [Fact]
    public async Task POST_parameters_returns_400_range_min_max_when_minimum_exceeds_maximum()
    {
        // VR-F07 — spec.md US2 acceptance scenario 7: Min = 100, Max = 50 blocks the save.
        var client = await _factory.SignedInClientAsync("P-01");

        var response = await client.PostAsJsonAsync(Route, new
        {
            name_en = "Bad Range",
            name_ar = "نطاق خاطئ",
            api_field = UniqueApiField("bad_range"),
            data_type = "range",
            range_min = 100,
            range_max = 50,
        });

        await response.ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).Should().Be("validation.range_min_max");
    }

    [Fact]
    public async Task POST_parameters_returns_400_invalid_data_type_for_a_type_outside_the_closed_list()
    {
        // [PO-G17] — "duration" and "identifier" were evaluated and REJECTED; they must not be accepted anywhere.
        var client = await _factory.SignedInClientAsync("P-01");

        var response = await client.PostAsJsonAsync(Route, new
        {
            name_en = "Handling Duration",
            name_ar = "مدة المعالجة",
            api_field = UniqueApiField("handling"),
            data_type = "duration",
        });

        await response.ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).Should().Be("validation.invalid_data_type");
    }

    [Fact]
    public async Task POST_parameters_assigns_the_channel_as_supported_with_the_required_default_applied()
    {
        // FR-S6-05 — a channel pill adds the parameter as supported; BR-08 keeps the channel contract row (not
        // this seeded default) authoritative at request time.
        var client = await _factory.SignedInClientAsync("P-01");
        var channelId = await _factory.SeedServiceChannelAsync(nameEn: $"Assign target {Guid.NewGuid():N}"[..40]);

        var response = await client.PostAsJsonAsync(Route, new
        {
            name_en = "Queue Number",
            name_ar = "رقم الدور",
            api_field = UniqueApiField("queue_no"),
            data_type = "number",
            required_by_default = true,
            channel_ids = new[] { channelId },
        });

        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        var id = (await response.ReadJsonAsync()).GetProperty("id").GetGuid();

        (await _factory.CountRowsAsync(
                "channel_parameter_assignments",
                $"parameter_id = '{id}' AND service_channel_id = '{channelId}' AND supported AND required"))
            .Should().Be(1);
    }

    [Fact]
    public async Task PATCH_parameters_disables_an_unreferenced_parameter_without_a_warning()
    {
        // BR-10 — no references means no Dialog D-6: the change applies immediately.
        var (client, actor) = await _factory.SignedInWithActorAsync("P-01");
        var id = await _factory.SeedCustomParameterAsync(nameEn: "Unreferenced", apiField: UniqueApiField("unref"));

        var response = await client.PatchAsJsonAsync($"{Route}/{id}", new { enabled = false });

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        body.GetProperty("requires_confirmation").GetBoolean().Should().BeFalse();
        body.GetProperty("references").GetArrayLength().Should().Be(0);
        body.GetProperty("parameter").GetProperty("enabled").GetBoolean().Should().BeFalse();

        (await _factory.CountEventsAsync(actor.UserId, "parameter.disabled")).Should().Be(1);
    }

    [Fact]
    public async Task PATCH_parameters_withholds_the_disable_and_returns_the_reference_list_when_a_channel_uses_it()
    {
        // AC-S5-02 / BR-10 — "the impact warning lists that reference BEFORE anything changes". The assertion
        // that matters is the second half: the parameter must still be ENABLED after this call.
        var client = await _factory.SignedInClientAsync("P-01");
        var channelName = $"Referencing channel {Guid.NewGuid():N}"[..40];
        var channelId = await _factory.SeedServiceChannelAsync(nameEn: channelName);
        var parameterId = await _factory.SeedCustomParameterAsync(
            nameEn: "Referenced", apiField: UniqueApiField("referenced"));
        await _factory.SeedChannelParameterAssignmentAsync(channelId, parameterId);

        var response = await client.PatchAsJsonAsync($"{Route}/{parameterId}", new { enabled = false });

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        body.GetProperty("requires_confirmation").GetBoolean().Should().BeTrue();
        body.GetProperty("parameter").GetProperty("enabled").GetBoolean()
            .Should().BeTrue("BR-10's warning must precede the change, not follow it");

        var references = body.GetProperty("references").EnumerateArray().ToList();
        references.Should().ContainSingle();
        references[0].GetProperty("kind").GetString().Should().Be("channel_contract");
        references[0].GetProperty("name").GetString().Should().Be(channelName);

        // The withheld change left no trace in storage either.
        (await _factory.CountRowsAsync("parameters", $"id = '{parameterId}' AND enabled")).Should().Be(1);
    }

    [Fact]
    public async Task PATCH_parameters_applies_the_disable_once_the_impact_warning_is_confirmed()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var channelId = await _factory.SeedServiceChannelAsync(nameEn: $"Confirm channel {Guid.NewGuid():N}"[..40]);
        var parameterId = await _factory.SeedCustomParameterAsync(
            nameEn: "Confirmed", apiField: UniqueApiField("confirmed"));
        await _factory.SeedChannelParameterAssignmentAsync(channelId, parameterId);

        var response = await client.PatchAsJsonAsync(
            $"{Route}/{parameterId}", new { enabled = false, confirm_disable = true });

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        body.GetProperty("requires_confirmation").GetBoolean().Should().BeFalse();
        body.GetProperty("parameter").GetProperty("enabled").GetBoolean().Should().BeFalse();

        // The list still travels back on the applied call — informational, so the console can report what changed.
        body.GetProperty("references").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task PATCH_parameters_returns_409_parameter_type_locked_when_a_built_in_type_is_changed()
    {
        // [PO-G27] / BR-09 — a built-in's data type is read-only, enforced server-side regardless of client state.
        var client = await _factory.SignedInClientAsync("P-01");
        var branch = await _factory.GetParameterIdByApiFieldAsync("branch");

        var response = await client.PatchAsJsonAsync($"{Route}/{branch}", new { data_type = "text" });

        await response.ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).Should().Be("parameter.type_locked");
    }

    [Fact]
    public async Task PATCH_parameters_returns_409_api_field_locked_when_a_built_in_is_renamed()
    {
        // BR-09 / VR-F06 — the API field name of a built-in is permanently read-only.
        var client = await _factory.SignedInClientAsync("P-01");
        var region = await _factory.GetParameterIdByApiFieldAsync("region");

        var response = await client.PatchAsJsonAsync($"{Route}/{region}", new { api_field = "region_code" });

        await response.ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).Should().Be("parameter.api_field_locked");
    }

    [Fact]
    public async Task PATCH_parameters_allows_editing_a_built_in_display_names_and_usage_flags()
    {
        // The other half of BR-09: "never renamed" is about the API FIELD. A tenant may still relabel a built-in
        // and change its usage flags — and the read-only fields it omits must not be read as a change.
        var client = await _factory.SignedInClientAsync("P-01");
        var employee = await _factory.GetParameterIdByApiFieldAsync("employee");

        var response = await client.PatchAsJsonAsync($"{Route}/{employee}", new
        {
            name_en = "Staff Member",
            dashboard_visibility = true,
        });

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        var parameter = (await response.ReadJsonAsync()).GetProperty("parameter");
        parameter.GetProperty("name_en").GetString().Should().Be("Staff Member");
        parameter.GetProperty("dashboard_visibility").GetBoolean().Should().BeTrue();
        parameter.GetProperty("api_field").GetString().Should().Be("employee");
        parameter.GetProperty("api_field_locked").GetBoolean().Should().BeTrue();
        parameter.GetProperty("data_type_locked").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task PATCH_parameters_returns_409_api_field_locked_when_a_request_has_already_carried_the_field()
    {
        // BR-11's LIVE probe (not just the persisted flag): the parameter below has api_field_locked = false, and
        // the lock comes purely from a logged request whose parameters_received carries the key.
        var client = await _factory.SignedInClientAsync("P-01");
        var apiField = UniqueApiField("live_lock");
        var id = await _factory.SeedCustomParameterAsync(
            nameEn: "Live Locked", apiField: apiField, apiFieldLocked: false);

        var channelId = await _factory.SeedServiceChannelAsync(nameEn: $"Traffic channel {Guid.NewGuid():N}"[..40]);
        var integrationId = await _factory.SeedIntegrationAsync(channelId, name: $"Traffic {Guid.NewGuid():N}"[..30]);
        await _factory.SeedRequestLogAsync(
            integrationId, parametersReceived: JsonSerializer.Serialize(new Dictionary<string, string>
            {
                [apiField] = "42",
            }));

        var response = await client.PatchAsJsonAsync($"{Route}/{id}", new { api_field = UniqueApiField("renamed") });

        await response.ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).Should().Be("parameter.api_field_locked");
    }

    [Fact]
    public async Task PATCH_parameters_renames_an_unlocked_custom_field_when_no_request_has_carried_it()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var id = await _factory.SeedCustomParameterAsync(
            nameEn: "Renameable", apiField: UniqueApiField("renameable"));
        var newField = UniqueApiField("renamed_ok");

        var response = await client.PatchAsJsonAsync($"{Route}/{id}", new { api_field = newField });

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        (await response.ReadJsonAsync()).GetProperty("parameter").GetProperty("api_field").GetString()
            .Should().Be(newField);
    }

    [Fact]
    public async Task PATCH_parameters_returns_404_when_the_parameter_does_not_exist()
    {
        var client = await _factory.SignedInClientAsync("P-01");

        var response = await client.PatchAsJsonAsync($"{Route}/{Guid.NewGuid()}", new { enabled = false });

        await response.ShouldHaveStatusAsync(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).Should().Be("parameter.not_found");
    }

    [Fact]
    public async Task GET_parameters_combines_the_origin_and_type_filters_with_AND_and_keeps_the_tab_counts_global()
    {
        // AC-S5-01 — "only custom Range parameters remain, while the tab counts stay global (unaffected by the
        // type filter)". Both halves are asserted: the items are filtered, the counts are not.
        var client = await _factory.SignedInClientAsync("P-01");
        var rangeField = UniqueApiField("filter_range");
        await _factory.SeedCustomParameterAsync(nameEn: "Filter Range", apiField: rangeField, dataType: "number");

        var response = await client.GetAsync($"{Route}?origin=custom&type=number&limit=200");

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();

        var items = body.GetProperty("items").EnumerateArray().ToList();
        items.Should().NotBeEmpty();
        items.Should().OnlyContain(item =>
            item.GetProperty("origin").GetString() == "custom"
            && item.GetProperty("data_type").GetString() == "number");
        items.Should().Contain(item => item.GetProperty("api_field").GetString() == rangeField);

        var counts = body.GetProperty("counts");
        counts.GetProperty("built_in").GetInt32().Should().Be(23, "BR-23 seeds exactly 23 built-ins");
        counts.GetProperty("all").GetInt32().Should()
            .Be(counts.GetProperty("built_in").GetInt32() + counts.GetProperty("custom").GetInt32());
        counts.GetProperty("all").GetInt32().Should().BeGreaterThan(items.Count,
            "the tab counts are global and must not shrink with the type filter");
    }

    [Fact]
    public async Task GET_parameters_filtered_to_built_in_returns_the_23_seeded_parameters_all_enabled()
    {
        // The Independent Test's first step: "verify the 'All · 23' built-ins are enabled" (BR-23).
        var client = await _factory.SignedInClientAsync("P-01");

        var response = await client.GetAsync($"{Route}?origin=built_in&limit=200");

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        var items = (await response.ReadJsonAsync()).GetProperty("items").EnumerateArray().ToList();

        items.Should().HaveCount(23);
        items.Should().OnlyContain(item =>
            item.GetProperty("origin").GetString() == "built_in"
            && item.GetProperty("api_field_locked").GetBoolean()
            && item.GetProperty("data_type_locked").GetBoolean());
        items.Where(item => item.GetProperty("enabled").GetBoolean()).Should().HaveCount(23);
    }

    [Fact]
    public async Task GET_parameters_searches_by_api_field_as_well_as_by_name()
    {
        // SCR-05's "Search by name or API field…" placeholder is a functional promise, not just copy.
        var client = await _factory.SignedInClientAsync("P-01");

        var byField = await client.GetAsync($"{Route}?q=journey_stage&limit=200");
        await byField.ShouldHaveStatusAsync(HttpStatusCode.OK);
        (await byField.ReadJsonAsync()).GetProperty("items").EnumerateArray()
            .Should().Contain(item => item.GetProperty("api_field").GetString() == "journey_stage");

        var byName = await client.GetAsync($"{Route}?q=nationality&limit=200");
        await byName.ShouldHaveStatusAsync(HttpStatusCode.OK);
        (await byName.ReadJsonAsync()).GetProperty("items").EnumerateArray()
            .Should().Contain(item => item.GetProperty("api_field").GetString() == "nationality");
    }

    [Fact]
    public async Task GET_parameters_returns_400_for_an_unrecognised_type_filter()
    {
        // An unknown literal must not be silently ignored — that would return the unfiltered list and read as a
        // data bug rather than a client error.
        var client = await _factory.SignedInClientAsync("P-01");

        var response = await client.GetAsync($"{Route}?type=duration");

        await response.ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).Should().Be("validation.invalid_data_type");
    }

    [Fact]
    public async Task DELETE_parameters_returns_405_because_no_delete_endpoint_exists()
    {
        // BR-09 — parameters of either origin are disabled, never deleted. The ABSENCE of the route is the
        // enforcement; this test fails the day someone adds one.
        var client = await _factory.SignedInClientAsync("P-01");
        var id = await _factory.SeedCustomParameterAsync(
            nameEn: "Never deletable", apiField: UniqueApiField("no_delete"));

        var response = await client.DeleteAsync($"{Route}/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        (await _factory.RowExistsAsync("parameters", id)).Should().BeTrue();
    }
}
