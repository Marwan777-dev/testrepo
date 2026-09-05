using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Nabadat.KpiManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace Nabadat.KpiManagement.IntegrationTests.Scenarios;

/// <summary>
/// T115 [US4] — multi-step business-cycle test for the US-4 Independent Test: a P-01 CX Program
/// Manager reads the seeded default scoring parameters, updates three of the five fields, re-reads to
/// confirm the change persisted, and the run ends with exactly one <c>journey.scoring_config.updated</c>
/// event whose <c>new_value</c> carries the three changed values. Proves the read → edit → persist
/// cycle holds together and emits a single audit event for the save.
/// </summary>
[Collection(KpiManagementIntegrationCollection.Name)]
public sealed class ScoringConfigEditAndPersistScenarioTests
{
    private const string ScoringConfigUpdatedEvent = "journey.scoring_config.updated";

    private readonly KpiManagementApplicationFactory _factory;

    public ScoringConfigEditAndPersistScenarioTests(KpiManagementApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task ProgramManager_reads_defaults_updates_three_fields_and_persists_with_one_event()
    {
        await _factory.ResetScoringConfigAsync();
        var (client, actor) = await _factory.SignedInWithActorAsync(PersonaContextHelper.CxProgramManager);

        // 1. Read the seeded defaults.
        var defaults = await client.GetAsync("/api/v1/tenant/scoring-config");
        defaults.StatusCode.Should().Be(HttpStatusCode.OK);
        var before = await defaults.ReadJsonAsync();
        before.GetProperty("alpha").GetDecimal().Should().Be(0.500m);
        before.GetProperty("mot_multiplier").GetDecimal().Should().Be(1.5m);
        before.GetProperty("n_floor").GetInt32().Should().Be(100);

        // 2. Update three of the five fields (alpha, mot_multiplier, n_floor); leave the other two at default.
        var update = await client.PutAsJsonAsync("/api/v1/tenant/scoring-config", new
        {
            alpha = 0.700m,
            mot_multiplier = 1.8m,
            n_floor = 250,
            flag_percentile = 25,
            rolling_window_days = 30,
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Re-read and confirm the three changes persisted (β re-derived to 0.300), others unchanged.
        var after = await client.GetAsync("/api/v1/tenant/scoring-config");
        var body = await after.ReadJsonAsync();
        body.GetProperty("alpha").GetDecimal().Should().Be(0.700m);
        body.GetProperty("beta").GetDecimal().Should().Be(0.300m);
        body.GetProperty("mot_multiplier").GetDecimal().Should().Be(1.8m);
        body.GetProperty("n_floor").GetInt32().Should().Be(250);
        body.GetProperty("flag_percentile").GetInt32().Should().Be(25);
        body.GetProperty("rolling_window_days").GetInt32().Should().Be(30);

        // 4. Exactly one journey.scoring_config.updated event for this actor, carrying the three new values.
        (await _factory.CountEventsAsync(actor.UserId, ScoringConfigUpdatedEvent)).Should().Be(1);

        var newValueText = await _factory.LatestEventNewValueAsync(actor.UserId, ScoringConfigUpdatedEvent);
        newValueText.Should().NotBeNull();
        using var newValue = JsonDocument.Parse(newValueText!);
        var root = newValue.RootElement;
        GetNumber(root, "Alpha").Should().Be(0.700m);
        GetNumber(root, "MotMultiplier").Should().Be(1.8m);
        GetNumber(root, "NFloor").Should().Be(250m);
    }

    /// <summary>Reads a numeric property from the event <c>new_value</c> jsonb, case-insensitively.</summary>
    private static decimal GetNumber(JsonElement root, string name)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value.GetDecimal();
            }
        }

        throw new Xunit.Sdk.XunitException($"Property '{name}' not found in new_value: {root.GetRawText()}");
    }
}
