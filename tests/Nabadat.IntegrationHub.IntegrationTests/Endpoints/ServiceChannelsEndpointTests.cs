using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Nabadat.IntegrationHub.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.IntegrationHub.IntegrationTests.Endpoints;

/// <summary>
/// T041 [US1] — HTTP-level tests for the service-channel endpoints (SCR-03/04,
/// contracts/api-endpoints.md). Covers spec.md US1's Integration Test Coverage: create → 201 + the
/// <c>channel.created</c> audit row, case-insensitive duplicate name/ID → 409, a pre-lock ID edit → 200, a
/// post-lock ID edit → 409 (BR-05, from <i>both</i> lock sources), deactivation → 200, and the SCR-03 list
/// counts.
///
/// <para>The deactivation <b>cascade</b> (a serving integration's endpoint then returning <c>E-1004</c>) is
/// deliberately NOT asserted here — it needs US4's request pipeline and is covered end-to-end by that story's
/// scenario test, per spec.md's own note. This file asserts only that the status flip itself persists.</para>
///
/// <para><b>Shared-fixture hygiene:</b> this lane writes real rows and never rolls back, so every channel here
/// takes a unique EN name and channel ID — the two case-insensitive unique indexes would otherwise make tests
/// collide with each other rather than with their own arrangement. Note also that VR-F13 caps a tenant at 100
/// channels, so the create-path tests depend on the shared container staying under that ceiling
/// (TODO-M13-004).</para>
/// </summary>
[Collection(IntegrationHubIntegrationCollection.Name)]
public sealed class ServiceChannelsEndpointTests
{
    private const string Route = "/api/v1/integration-hub/service-channels";

    private readonly IntegrationHubApplicationFactory _factory;

    public ServiceChannelsEndpointTests(IntegrationHubApplicationFactory factory) => _factory = factory;

    /// <summary>
    /// A unique EN name inside VR-F02's 50-character cap. An 8-hex-character suffix is enough to keep the
    /// shared container's case-insensitive unique index happy across a run, and short enough that no prefix
    /// here can push the name past 50 and trip the validator instead of the assertion under test.
    /// </summary>
    private static string UniqueName(string prefix) =>
        Truncate($"{prefix} {Guid.NewGuid():N}"[..(prefix.Length + 9)], 50);

    /// <summary>A unique ID inside VR-F04's <c>[A-Za-z0-9-]</c>, 19-character envelope.</summary>
    private static string UniqueChannelId(string prefix) =>
        Truncate($"{prefix}{Guid.NewGuid():N}", 19);

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static string ArabicName => "قناة الخدمة";

    [Fact]
    public async Task POST_service_channels_returns_201_with_contract_and_emits_channel_created_when_input_is_valid()
    {
        var (client, actor) = await _factory.SignedInWithActorAsync("P-01");
        var mobile = await _factory.GetParameterIdByApiFieldAsync("mobile");
        var email = await _factory.GetParameterIdByApiFieldAsync("email");

        var response = await client.PostAsJsonAsync(Route, new
        {
            name_en = UniqueName("Self-Service Kiosk"),
            name_ar = ArabicName,
            channel_id = UniqueChannelId("KIOSK"),
            description = "Lobby kiosks",
            active = true,
            contract = new[]
            {
                new { parameter_id = mobile, supported = true, required = true },
                new { parameter_id = email, supported = true, required = false },
            },
        });

        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        var body = await response.ReadJsonAsync();
        body.GetProperty("supported_count").GetInt32().Should().Be(2);
        body.GetProperty("required_count").GetInt32().Should().Be(1);
        body.GetProperty("integrations_count").GetInt32().Should().Be(0);
        body.GetProperty("channel_id_locked").GetBoolean().Should().BeFalse();
        body.GetProperty("contract").GetArrayLength().Should().Be(2);

        // The row is readable through GET /{id} with its contract intact.
        var id = body.GetProperty("id").GetString();
        var get = await client.GetAsync($"{Route}/{id}");
        await get.ShouldHaveStatusAsync(HttpStatusCode.OK);
        (await get.ReadJsonAsync()).GetProperty("contract").GetArrayLength().Should().Be(2);

        // Exactly one M-17 audit row for this actor, written in the same transaction as the channel (DB-08).
        (await _factory.CountEventsAsync(actor.UserId, "channel.created")).Should().Be(1);
    }

    [Fact]
    public async Task POST_service_channels_stores_only_the_supported_rows_when_the_contract_has_unsupported_ones()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var mobile = await _factory.GetParameterIdByApiFieldAsync("mobile");
        var email = await _factory.GetParameterIdByApiFieldAsync("email");

        // FR-S4-04: the unsupported row's Required is force-cleared, and a row with neither flag carries no
        // signal, so it is not persisted — the counts and the contract table stay meaningful.
        var response = await client.PostAsJsonAsync(Route, new
        {
            name_en = UniqueName("Contract normalisation"),
            name_ar = ArabicName,
            channel_id = UniqueChannelId("NORM"),
            contract = new[]
            {
                new { parameter_id = mobile, supported = true, required = true },
                new { parameter_id = email, supported = false, required = true },
            },
        });

        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        var body = await response.ReadJsonAsync();
        body.GetProperty("supported_count").GetInt32().Should().Be(1);
        body.GetProperty("required_count").GetInt32().Should().Be(1);
        body.GetProperty("contract").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task POST_service_channels_sanitizes_the_channel_id_when_the_raw_value_has_invalid_characters()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var suffix = Guid.NewGuid().ToString("N")[..8];

        // VR-F04 is enforced server-side too, so a caller bypassing the console gets the same sanitised ID
        // instead of a column CHECK violation surfacing as a 500.
        var response = await client.PostAsJsonAsync(Route, new
        {
            name_en = UniqueName("Raw id"),
            name_ar = ArabicName,
            channel_id = $"My kiosk #{suffix}",
        });

        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        (await response.ReadJsonAsync()).GetProperty("channel_id").GetString()
            .Should().Be($"Mykiosk{suffix}");
    }

    [Fact]
    public async Task POST_service_channels_returns_409_duplicate_channel_id_when_the_id_differs_only_by_case()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var channelId = UniqueChannelId("DUP");
        await _factory.SeedServiceChannelAsync(UniqueName("Existing id owner"), ArabicName, channelId);

        var response = await client.PostAsJsonAsync(Route, new
        {
            name_en = UniqueName("Case clash"),
            name_ar = ArabicName,
            channel_id = channelId.ToLowerInvariant(),
        });

        await response.ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).Should().Be("validation.duplicate_channel_id");
    }

    [Fact]
    public async Task POST_service_channels_returns_409_duplicate_name_when_the_english_name_differs_only_by_case()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var nameEn = UniqueName("Self-Service Kiosk");
        await _factory.SeedServiceChannelAsync(nameEn, ArabicName, UniqueChannelId("NAME"));

        var response = await client.PostAsJsonAsync(Route, new
        {
            name_en = nameEn.ToLowerInvariant(),
            name_ar = ArabicName,
            channel_id = UniqueChannelId("NAME"),
        });

        await response.ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).Should().Be("validation.duplicate_name");
    }

    [Fact]
    public async Task POST_service_channels_returns_400_with_every_inline_error_when_both_names_are_missing()
    {
        var client = await _factory.SignedInClientAsync("P-01");

        var response = await client.PostAsJsonAsync(Route, new
        {
            name_en = "",
            name_ar = "",
            channel_id = UniqueChannelId("MISSING"),
        });

        await response.ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
        // Accumulated failures travel in the API-05 envelope's details so SCR-04 renders both inline at once.
        (await response.ReadErrorDetailCodesAsync()).Should()
            .Contain(new[] { "validation.name_en_required", "validation.name_ar_required" });
    }

    [Fact]
    public async Task POST_service_channels_returns_400_channel_id_required_when_every_character_is_stripped()
    {
        var client = await _factory.SignedInClientAsync("P-01");

        var response = await client.PostAsJsonAsync(Route, new
        {
            name_en = UniqueName("All invalid id"),
            name_ar = ArabicName,
            channel_id = "### $$$",
        });

        await response.ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).Should().Be("validation.channel_id_required");
    }

    [Fact]
    public async Task PUT_service_channels_returns_200_and_changes_the_id_when_no_successful_request_was_logged()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var id = await _factory.SeedServiceChannelAsync(
            UniqueName("Pre-lock edit"), ArabicName, UniqueChannelId("PRE"));
        var newChannelId = UniqueChannelId("MOVED");

        // BR-05: pre-lock the ID is editable and the edit changes the endpoint path.
        var response = await client.PutAsJsonAsync($"{Route}/{id}", new
        {
            name_en = UniqueName("Pre-lock edited"),
            name_ar = ArabicName,
            channel_id = newChannelId,
            active = true,
        });

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        (await response.ReadJsonAsync()).GetProperty("channel_id").GetString().Should().Be(newChannelId);
    }

    [Fact]
    public async Task PUT_service_channels_returns_409_id_locked_when_the_persisted_lock_flag_is_set()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var id = await _factory.SeedServiceChannelAsync(
            UniqueName("Locked flag"), ArabicName, UniqueChannelId("LOCKED"), channelIdLocked: true);

        var response = await client.PutAsJsonAsync($"{Route}/{id}", new
        {
            name_en = UniqueName("Locked flag edit"),
            name_ar = ArabicName,
            channel_id = UniqueChannelId("NEWID"),
            active = true,
        });

        await response.ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).Should().Be("channel.id_locked");
    }

    [Fact]
    public async Task PUT_service_channels_returns_409_id_locked_when_traffic_exists_but_the_flag_was_never_written()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var id = await _factory.SeedServiceChannelAsync(
            UniqueName("Live probe lock"), ArabicName, UniqueChannelId("PROBE"), channelIdLocked: false);
        var integrationId = await _factory.SeedIntegrationAsync(id, UniqueName("Probe integration"));
        await _factory.SeedRequestLogAsync(integrationId, httpStatus: 202, resultCode: "202");

        // The live "has this channel logged a 2xx?" probe is the guard's second, independent lock source.
        var response = await client.PutAsJsonAsync($"{Route}/{id}", new
        {
            name_en = UniqueName("Live probe edit"),
            name_ar = ArabicName,
            channel_id = UniqueChannelId("PROBE2"),
            active = true,
        });

        await response.ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).Should().Be("channel.id_locked");
    }

    [Fact]
    public async Task PUT_service_channels_returns_200_when_a_locked_channel_is_renamed_without_touching_the_id()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var channelId = UniqueChannelId("RENAME");
        var id = await _factory.SeedServiceChannelAsync(
            UniqueName("Locked rename"), ArabicName, channelId, channelIdLocked: true);
        var renamed = UniqueName("Locked renamed");

        // BR-06: renaming never affects the ID, so a rename must still save on a locked channel.
        var response = await client.PutAsJsonAsync($"{Route}/{id}", new
        {
            name_en = renamed,
            name_ar = ArabicName,
            channel_id = channelId,
            active = true,
        });

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        body.GetProperty("name_en").GetString().Should().Be(renamed);
        body.GetProperty("channel_id").GetString().Should().Be(channelId);
    }

    [Fact]
    public async Task PUT_service_channels_returns_200_and_persists_inactive_when_active_is_set_false()
    {
        var (client, actor) = await _factory.SignedInWithActorAsync("P-01");
        var channelId = UniqueChannelId("DEACT");
        var id = await _factory.SeedServiceChannelAsync(UniqueName("Deactivating"), ArabicName, channelId);

        var response = await client.PutAsJsonAsync($"{Route}/{id}", new
        {
            name_en = UniqueName("Deactivated"),
            name_ar = ArabicName,
            channel_id = channelId,
            active = false,
        });

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        (await response.ReadJsonAsync()).GetProperty("active").GetBoolean().Should().BeFalse();

        // The status flip gets its own audit row alongside the generic channel.updated one.
        (await _factory.CountEventsAsync(actor.UserId, "channel.deactivated")).Should().Be(1);
        (await _factory.CountEventsAsync(actor.UserId, "channel.updated")).Should().Be(1);
    }

    [Fact]
    public async Task PUT_service_channels_returns_404_when_the_channel_does_not_exist()
    {
        var client = await _factory.SignedInClientAsync("P-01");

        var response = await client.PutAsJsonAsync($"{Route}/{Guid.NewGuid()}", new
        {
            name_en = UniqueName("Ghost"),
            name_ar = ArabicName,
            active = true,
        });

        await response.ShouldHaveStatusAsync(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).Should().Be("channel.not_found");
    }

    [Fact]
    public async Task GET_service_channels_returns_supported_required_and_integration_counts_for_the_row()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var id = await _factory.SeedServiceChannelAsync(
            UniqueName("Counted channel"), ArabicName, UniqueChannelId("COUNT"));
        var mobile = await _factory.GetParameterIdByApiFieldAsync("mobile");
        var email = await _factory.GetParameterIdByApiFieldAsync("email");
        var vip = await _factory.GetParameterIdByApiFieldAsync("vip");

        await _factory.SeedChannelParameterAssignmentAsync(id, mobile, supported: true, required: true);
        await _factory.SeedChannelParameterAssignmentAsync(id, email, supported: true, required: false);
        await _factory.SeedChannelParameterAssignmentAsync(id, vip, supported: true, required: false);
        await _factory.SeedIntegrationAsync(id, UniqueName("Counted integration"));

        var row = await FindListRowAsync(client, id);

        row.Should().NotBeNull();
        row!.Value.GetProperty("supported_count").GetInt32().Should().Be(3);
        row.Value.GetProperty("required_count").GetInt32().Should().Be(1);
        row.Value.GetProperty("integrations_count").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task DELETE_service_channels_is_not_routed_because_no_delete_operation_exists()
    {
        var client = await _factory.SignedInClientAsync("P-01");
        var id = await _factory.SeedServiceChannelAsync(
            UniqueName("Undeletable"), ArabicName, UniqueChannelId("NODEL"));

        var response = await client.DeleteAsync($"{Route}/{id}");

        // BR-07 / FR-S3-02: deactivate only. The absence of the route IS the enforcement — the path matches
        // GET/PUT, so an unsupported verb is rejected by routing itself, and the channel survives.
        await response.ShouldHaveStatusAsync(HttpStatusCode.MethodNotAllowed);
        (await _factory.RowExistsAsync("service_channels", id)).Should().BeTrue();
    }

    /// <summary>
    /// Walks the cursor pages until the channel with <paramref name="id"/> is found, so the assertion survives a
    /// shared container holding more channels than one page. Returns <c>null</c> if it is absent.
    /// </summary>
    private static async Task<JsonElement?> FindListRowAsync(HttpClient client, Guid id)
    {
        string? cursor = null;

        do
        {
            var query = cursor is null ? $"{Route}?limit=200" : $"{Route}?limit=200&cursor={cursor}";
            var response = await client.GetAsync(query);
            await response.ShouldHaveStatusAsync(HttpStatusCode.OK);

            var body = await response.ReadJsonAsync();
            foreach (var item in body.GetProperty("items").EnumerateArray())
            {
                if (item.GetProperty("id").GetGuid() == id)
                {
                    return item.Clone();
                }
            }

            cursor = body.TryGetProperty("next_cursor", out var next) && next.ValueKind == JsonValueKind.String
                ? next.GetString()
                : null;
        }
        while (cursor is not null);

        return null;
    }
}
