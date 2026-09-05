using Microsoft.Playwright;
using Microsoft.Playwright.MSTest;
using Nabadat.E2ETests.Infrastructure;

namespace Nabadat.E2ETests.IntegrationHub;

/// <summary>
/// US2 browser E2E coverage for the M-13 parameter catalogue — SCR-05 (<c>/integration-hub/
/// parameters</c>) and the SCR-06 drawer that opens over it (no route of its own) — per spec.md
/// US2's "E2E Test Coverage" block and the E2E Test Policy. Drives the real MFA-gated SPA as the
/// owning persona (P-01, CX Manager) and selects on stable <c>data-testid</c> / <c>id</c> hooks,
/// never translated text (the SPA is bilingual ar/en and RTL by default).
///
/// <para><b>Run prerequisites</b> (COVERAGE.md): the stack up (Postgres + the
/// <c>Nabadat.TenantAdmin</c> host + <c>npm run dev</c>) with the M-13 baseline applied to the e2e
/// tenant schema — the 8 tables <b>and the 23 seeded built-in parameters</b>, which three of these
/// scenarios read directly — <c>E2E_BASE_URL</c> pointing at THIS checkout's dev server, and the
/// seeded per-persona credentials in the gitignored <c>appsettings.local.json</c>.</para>
///
/// <para><b>These tests write real rows</b> — the E2E lane has no transaction rollback, and VR-F13
/// caps a tenant at 200 <i>custom</i> parameters. Every parameter a test seeds or creates is torn
/// down in <see cref="CleanUpAsync"/> via <see cref="E2ETenantDb"/>, and every API field carries a
/// run-unique suffix so a leaked row from an earlier run can never collide with this one.</para>
/// </summary>
[TestClass]
public sealed class ParameterCatalogueTests : E2ETestBase
{
    private const string ParametersRoute = "/integration-hub/parameters";

    private E2ETenantDb Db => new(Settings);

    private readonly List<Guid> _seededParameterIds = [];
    private readonly List<string> _createdApiFields = [];
    private readonly List<Guid> _seededChannelIds = [];

    /// <summary>Run-unique suffix. Digits only, so it stays legal inside a snake_case API field.</summary>
    private static string Unique() => DateTime.UtcNow.ToString("HHmmssfff");

    [TestCleanup]
    public async Task CleanUpAsync()
    {
        if (!Db.IsConfigured)
        {
            return;
        }

        foreach (var apiField in _createdApiFields)
        {
            await Db.DeleteCustomParameterByApiFieldAsync(apiField);
        }

        foreach (var id in _seededParameterIds)
        {
            await Db.DeleteParameterAsync(id);
        }

        foreach (var id in _seededChannelIds)
        {
            await Db.DeleteServiceChannelAsync(id);
        }
    }

    private async Task GoToListAsync(string persona = "P-01")
    {
        await SignInAsync(persona);
        await Page.GotoAsync($"{BaseUrl}{ParametersRoute}");
        await Page.GetByTestId("parameter-search").WaitForAsync();
        // The filter row paints before the first response lands, so waiting on the search box is
        // NOT enough — the origin-tab count pills are deliberately absent until the counts arrive
        // (a loading tab shows its label, never a flash of "0"). Wait for a digit in the All tab so
        // a count read can't race the fetch.
        await Expect(Page.GetByTestId("tab-all")).ToContainTextAsync(
            new System.Text.RegularExpressions.Regex(@"\d"),
            new LocatorAssertionsToContainTextOptions { Timeout = 15000 });
    }

    /// <summary>Opens the SCR-06 drawer in create mode and waits for its first field.</summary>
    private async Task OpenNewParameterDrawerAsync()
    {
        await GoToListAsync();
        await Page.GetByTestId("new-parameter").ClickAsync();
        await Page.GetByTestId("parameter-name-en").WaitForAsync();
    }

    /// <summary>
    /// Picks a data type in the drawer's select. base-ui renders the popup in a portal, so the
    /// option is addressed by its own testid rather than through the trigger's subtree.
    /// </summary>
    private async Task ChooseTypeAsync(string type)
    {
        await Page.GetByTestId("parameter-type").ClickAsync();
        await Page.GetByTestId($"parameter-type-option-{type}").ClickAsync();
    }

    // ── AC-S6-01 ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Parameters_type_switch_between_range_and_list_shows_correct_panel()
    {
        await OpenNewParameterDrawerAsync();

        var rangeCard = Page.GetByTestId("parameter-range-card");
        var listPanel = Page.GetByTestId("parameter-list-panel");

        // The seeded default is Text — neither conditional panel is on screen.
        await Expect(rangeCard).ToHaveCountAsync(0);
        await Expect(listPanel).ToHaveCountAsync(0);

        await ChooseTypeAsync("range");
        await Expect(rangeCard).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("parameter-range-min")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("parameter-range-max")).ToBeVisibleAsync();
        await Expect(listPanel).ToHaveCountAsync(0);

        // …"and vice versa": switching to List must swap the panels, not stack them.
        await ChooseTypeAsync("list");
        await Expect(listPanel).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("parameter-open-mappings")).ToBeVisibleAsync();
        await Expect(rangeCard).ToHaveCountAsync(0);

        // BR-27 — List forces Mapping support on and takes the control away from the user.
        var mappingFlag = Page.GetByTestId("parameter-flag-mappingSupport");
        await Expect(mappingFlag).ToBeCheckedAsync();
        await Expect(mappingFlag).ToBeDisabledAsync();
    }

    // ── AC-S6-02 ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Parameters_api_field_auto_suggests_from_english_name()
    {
        await OpenNewParameterDrawerAsync();

        var apiField = Page.GetByTestId("parameter-api-field");
        await Page.GetByTestId("parameter-name-en").FillAsync("Wait Time");

        await Expect(apiField).ToHaveValueAsync("wait_time");

        // The rule is exactly: lowercase, spaces → "_", every other non-alphanumeric **stripped**
        // — NOT transliterated and NOT turned into a separator. So the hyphen in "Wait-Time" and
        // the "!!" both vanish rather than becoming underscores.
        await Page.GetByTestId("parameter-name-en").FillAsync("Branch Wait-Time!! 2026");
        await Expect(apiField).ToHaveValueAsync("branch_waittime_2026");

        // "…and remains manually editable **before** first use": the field is not read-only, and
        // once the user takes it over a later keystroke in the EN name must not overwrite them.
        await Expect(apiField).Not.ToHaveAttributeAsync("readonly", string.Empty);
        await apiField.FillAsync("my_own_key");
        await Page.GetByTestId("parameter-name-en").FillAsync("Something Entirely Different");
        await Expect(apiField).ToHaveValueAsync("my_own_key");
    }

    // ── AC-S6-03 / VR-F06 ─────────────────────────────────────────────────────

    [TestMethod]
    public async Task Parameters_blocks_save_on_duplicate_api_field_including_disabled()
    {
        if (!Db.IsConfigured)
        {
            Assert.Inconclusive(
                "Tenant DB connection not configured (e2e.tenantDb). VR-F06 must bite against a "
                + "DISABLED parameter too, and this test seeds that row directly rather than "
                + "leaning on the inline enable/disable toggle a different scenario owns.");
            return;
        }

        var suffix = Unique();
        var taken = $"e2e_dup_{suffix}";
        // Deliberately DISABLED: uniqueness spans built-in + custom + enabled + disabled (VR-F06),
        // and a disabled row is the case a naive "WHERE enabled" query would miss.
        var seededId = await Db.SeedParameterAsync(
            nameEn: $"E2E Dup {suffix}",
            nameAr: $"مكرر {suffix}",
            apiField: taken,
            enabled: false);
        _seededParameterIds.Add(seededId);

        await OpenNewParameterDrawerAsync();
        await Page.GetByTestId("parameter-name-en").FillAsync($"E2E Dup Other {suffix}");
        await Page.GetByTestId("parameter-name-ar").FillAsync($"مكرر آخر {suffix}");
        await Page.GetByTestId("parameter-api-field").FillAsync(taken);
        _createdApiFields.Add(taken); // in case the server ever accepts it

        await Page.GetByTestId("parameter-save").ClickAsync();

        // Rejected inline: an error is announced and the drawer stays open on the same field.
        await Expect(Page.GetByRole(AriaRole.Alert).First).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 10000 });
        await Expect(Page.GetByTestId("parameter-drawer")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("parameter-api-field")).ToHaveValueAsync(taken);
    }

    // ── AC-S5-01 ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Parameters_origin_and_type_filters_combine_with_AND()
    {
        if (!Db.IsConfigured)
        {
            Assert.Inconclusive(
                "Tenant DB connection not configured (e2e.tenantDb); the AND assertion needs a "
                + "known custom Range parameter to survive both filters.");
            return;
        }

        var suffix = Unique();
        var rangeField = $"e2e_range_{suffix}";
        var textField = $"e2e_text_{suffix}";
        _seededParameterIds.Add(await Db.SeedParameterAsync(
            nameEn: $"E2E Range {suffix}", nameAr: $"نطاق {suffix}", apiField: rangeField,
            dataType: "range", rangeMin: 0, rangeMax: 120, rangeUnit: "minutes"));
        // A custom NON-Range row: it survives the origin filter alone, so its disappearance is
        // what proves the two filters are AND-combined rather than OR-ed.
        _seededParameterIds.Add(await Db.SeedParameterAsync(
            nameEn: $"E2E Text {suffix}", nameAr: $"نص {suffix}", apiField: textField));

        await GoToListAsync();

        // Global counts BEFORE any filter — these must not move (AC-S5-01).
        var allCountBefore = await TabCountAsync("all");
        var builtInCountBefore = await TabCountAsync("built_in");
        var customCountBefore = await TabCountAsync("custom");
        Assert.IsTrue(builtInCountBefore >= 23,
            $"The baseline seeds 23 built-in parameters; the Built-in tab reported {builtInCountBefore}.");

        await Page.GetByTestId("tab-custom").ClickAsync();
        await Expect(Page.GetByTestId($"parameter-row-{rangeField}")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId($"parameter-row-{textField}")).ToBeVisibleAsync();

        await Page.GetByTestId("parameter-type-filter").ClickAsync();
        await Page.GetByTestId("parameter-type-filter-range").ClickAsync();

        // AND: custom ∧ range keeps the Range row and drops the Text row…
        await Expect(Page.GetByTestId($"parameter-row-{rangeField}")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId($"parameter-row-{textField}")).ToHaveCountAsync(0);
        // …and no built-in survives the origin half of the filter.
        await Expect(Page.GetByTestId("parameter-row-service")).ToHaveCountAsync(0);

        // The tab counts are global: they describe the catalogue, not the filtered page. A count
        // that moved when a filter was applied would read as a bug.
        Assert.AreEqual(allCountBefore, await TabCountAsync("all"), "The All tab count must stay global.");
        Assert.AreEqual(builtInCountBefore, await TabCountAsync("built_in"), "The Built-in tab count must stay global.");
        Assert.AreEqual(customCountBefore, await TabCountAsync("custom"), "The Custom tab count must stay global.");
    }

    // ── AC-S5-02 / BR-10 ──────────────────────────────────────────────────────

    [TestMethod]
    public async Task Parameters_disable_shows_impact_warning_when_referenced()
    {
        if (!Db.IsConfigured)
        {
            Assert.Inconclusive(
                "Tenant DB connection not configured (e2e.tenantDb); the reference D-6 reports is "
                + "a channel-contract row, seeded here rather than built through SCR-04.");
            return;
        }

        var suffix = Unique();
        var apiField = $"e2e_ref_{suffix}";
        var channelId = await Db.SeedServiceChannelAsync(
            nameEn: $"E2E Ref Channel {suffix}",
            nameAr: $"قناة مرجعية {suffix}",
            channelId: $"E2E-REF-{suffix}");
        _seededChannelIds.Add(channelId);

        var parameterId = await Db.SeedParameterAsync(
            nameEn: $"E2E Referenced {suffix}", nameAr: $"معامل مرجعي {suffix}", apiField: apiField);
        _seededParameterIds.Add(parameterId);
        await Db.AssignParameterToChannelAsync(channelId, parameterId);

        await GoToListAsync();
        await Page.GetByTestId("parameter-search").FillAsync(apiField);
        var toggle = Page.GetByTestId($"enabled-{apiField}");
        await toggle.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await Expect(toggle).ToBeCheckedAsync();

        await toggle.ClickAsync();

        // BR-10: the warning lists the reference BEFORE anything changes — the server withheld the
        // write, so the row must still read as enabled behind the dialog.
        var dialog = Page.GetByTestId("parameter-impact-dialog");
        await Expect(dialog).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10000 });
        await Expect(dialog).ToContainTextAsync($"E2E Ref Channel {suffix}");
        await Expect(toggle).ToBeCheckedAsync();

        // Backing out leaves the parameter enabled — the warning is a gate, not a formality.
        await Page.GetByTestId("parameter-impact-cancel").ClickAsync();
        await Expect(dialog).ToHaveCountAsync(0);
        await Expect(toggle).ToBeCheckedAsync();
    }

    // ── BR-09 ─────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Parameters_builtin_row_has_no_delete_action_and_locked_api_field()
    {
        await GoToListAsync();

        // BR-09: built-ins are enable/disable only. There is no DELETE endpoint and no delete
        // affordance anywhere on the list — the absence IS the enforcement, so assert it broadly
        // (testid, plus the accessible name in both shipped locales) rather than on one hook.
        await Expect(Page.Locator("[data-testid*='delete' i]")).ToHaveCountAsync(0);
        await Expect(Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { NameString = "Delete" })).ToHaveCountAsync(0);
        await Expect(Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { NameString = "حذف" })).ToHaveCountAsync(0);

        // Open a built-in's editor. `service` is one of the 23 the baseline seeds (FR-F0-10).
        var editButton = Page.GetByTestId("edit-service");
        await editButton.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await editButton.ClickAsync();
        await Page.GetByTestId("parameter-drawer").WaitForAsync();

        await Expect(Page.GetByTestId("parameter-builtin-notice")).ToBeVisibleAsync();
        // Read-only, not disabled: the wire key must stay selectable and copyable (VR-F06/BR-09).
        await Expect(Page.GetByTestId("parameter-api-field")).ToHaveAttributeAsync("readonly", string.Empty);
        // `[PO-G27]` — a built-in's data type is read-only too.
        await Expect(Page.GetByTestId("parameter-type")).ToBeDisabledAsync();
        // …but the usage flags and display names stay editable (BR-09 allows exactly that).
        await Expect(Page.GetByTestId("parameter-name-en")).Not.ToHaveAttributeAsync("readonly", string.Empty);
        await Expect(Page.GetByTestId("parameter-flag-filterable")).ToBeEnabledAsync();
        // And still no delete inside the drawer, which is the other place one would naturally sit.
        await Expect(Page.Locator("[data-testid*='delete' i]")).ToHaveCountAsync(0);
    }

    // ── VR-F07 ────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Parameters_range_validation_blocks_min_greater_than_max()
    {
        var suffix = Unique();
        var apiField = $"e2e_badrange_{suffix}";

        await OpenNewParameterDrawerAsync();
        await Page.GetByTestId("parameter-name-en").FillAsync($"E2E Bad Range {suffix}");
        await Page.GetByTestId("parameter-name-ar").FillAsync($"نطاق خاطئ {suffix}");
        await Page.GetByTestId("parameter-api-field").FillAsync(apiField);
        _createdApiFields.Add(apiField); // in case the guard ever fails and the row is written

        await ChooseTypeAsync("range");
        await Page.GetByTestId("parameter-range-min").FillAsync("100");
        await Page.GetByTestId("parameter-range-max").FillAsync("50");

        await Page.GetByTestId("parameter-save").ClickAsync();

        // Blocked with an inline error; the drawer stays open and the values are preserved so the
        // user can correct them rather than retype the whole form.
        await Expect(Page.GetByRole(AriaRole.Alert).First).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("parameter-drawer")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("parameter-range-min")).ToHaveValueAsync("100");
        await Expect(Page.GetByTestId("parameter-range-max")).ToHaveValueAsync("50");

        // Correcting the pair clears the block — the rule is about the relationship, not the field.
        await Page.GetByTestId("parameter-range-max").FillAsync("500");
        await Page.GetByTestId("parameter-save").ClickAsync();
        await Expect(Page.GetByTestId("parameter-drawer")).ToHaveCountAsync(0,
            new LocatorAssertionsToHaveCountOptions { Timeout = 15000 });
    }

    /// <summary>
    /// Reads an origin tab's count pill. Returns -1 when the tab renders no pill at all, which is
    /// a real state (the pill is deliberately absent until the first response lands, so a loading
    /// tab shows its label rather than a flash of "0") and must not be read as zero.
    /// </summary>
    private async Task<int> TabCountAsync(string tab)
    {
        var text = await Page.GetByTestId($"tab-{tab}").InnerTextAsync();
        var match = System.Text.RegularExpressions.Regex.Match(text, @"\d+");
        return match.Success ? int.Parse(match.Value) : -1;
    }
}
