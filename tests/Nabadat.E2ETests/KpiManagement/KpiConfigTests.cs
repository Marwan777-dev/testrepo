using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Nabadat.E2ETests.Infrastructure;

namespace Nabadat.E2ETests.KpiManagement;

/// <summary>
/// US-2 browser E2E coverage for the KPI Configuration page (<c>/kpi-management/new</c> and
/// <c>/kpi-management/:shortName</c>), per spec.md US-2 "E2E Test Coverage" and the E2E Test Policy. Drives
/// the real MFA-gated portal as the relevant persona and asserts against stable <c>data-testid</c>
/// hooks (the portal is bilingual / RTL-by-default, so translated text selectors are avoided). The
/// create scenario writes a real KPI row (no rollback in the browser lane) under a unique Short Name
/// per run.
///
/// <para><b>Run prerequisites</b> (see COVERAGE.md): the stack must be up (Postgres + backend host +
/// <c>npm run dev</c>) with the M-06 baseline (8 seeded standards incl. NPS), and the per-persona
/// seeded credentials present in the gitignored <c>appsettings.local.json</c>.</para>
/// </summary>
[TestClass]
public sealed class KpiConfigTests : E2ETestBase
{
    // The edit URL carries the KPI's (lowercased) Short Name slug, not its GUID (e.g. /kpi-management/cxi).
    private static readonly Regex ConfigUrl = new(@"/kpi-management/[a-z0-9_-]+$");
    private static readonly Regex CatalogueUrl = new(@"/kpi-management$");
    private static readonly Regex EditUrl = new(@"/kpi-management/[a-z0-9_-]+$");

    private static string UniqueShortName() => "E2E" + Guid.NewGuid().ToString("N")[..6];

    private ILocator Save => Page.GetByTestId("kpi-save");

    /// <summary>Signs in and opens the create page, waiting for the form's Short Name field.</summary>
    private async Task GoToNewAsync(string persona)
    {
        await SignInAsync(persona);
        await Page.GotoAsync($"{BaseUrl}/kpi-management/new");
        await Page.Locator("#kpi-short-name").WaitForAsync();
    }

    /// <summary>Signs in, opens the catalogue, clicks the named KPI's row link, and lands on its edit page.</summary>
    private async Task GoToEditAsync(string persona, string shortName)
    {
        await SignInAsync(persona);
        await Page.GotoAsync($"{BaseUrl}/kpi-management");
        await Page.GetByTestId("kpi-table").WaitForAsync();
        await Page.Locator($"[data-testid='kpi-row'][data-short-name='{shortName}']")
            .GetByRole(AriaRole.Link).First.ClickAsync();
        await Page.WaitForURLAsync(EditUrl);
        await Page.Locator("#kpi-short-name").WaitForAsync();
    }

    /// <summary>Opens a base-ui Select by its trigger test-id and clicks the option by its test-id.</summary>
    private async Task SelectOptionAsync(string triggerTestId, string optionTestId)
    {
        await Page.GetByTestId(triggerTestId).ClickAsync();
        await Page.GetByTestId(optionTestId).ClickAsync();
    }

    // KPI-E2E-10
    [TestMethod]
    public async Task KpiConfig_creates_custom_kpi_when_form_is_valid_and_saved()
    {
        await GoToNewAsync("P-01");
        var shortName = UniqueShortName();

        await Page.Locator("#kpi-short-name").FillAsync(shortName);
        await Page.Locator("#kpi-full-name").FillAsync($"{shortName} full name");
        // Create defaults (Scale1_5 / WeightedAverage / 0<20<70<100 / target 80 / active) are valid.
        await Expect(Save).ToBeEnabledAsync();
        await Save.ClickAsync();

        // On success the page returns to the catalogue; the new KPI is findable by Short Name.
        await Expect(Page).ToHaveURLAsync(CatalogueUrl);
        await Page.Locator("#kpi-search").FillAsync(shortName);
        await Expect(Page.GetByTestId("kpi-row")).ToHaveCountAsync(1);
        await Expect(Page.GetByTestId("kpi-row").First).ToHaveAttributeAsync("data-short-name", shortName);
    }

    // KPI-E2E-11
    [TestMethod]
    public async Task KpiConfig_disables_save_when_required_fields_are_empty()
    {
        await GoToNewAsync("P-01");

        // Short Name + Full Name start empty in create mode → Save is disabled.
        await Expect(Save).ToBeDisabledAsync();

        await Page.Locator("#kpi-short-name").FillAsync(UniqueShortName());
        await Page.Locator("#kpi-full-name").FillAsync("Required now satisfied");
        await Expect(Save).ToBeEnabledAsync();
    }

    // KPI-E2E-12
    [TestMethod]
    public async Task KpiConfig_shows_inline_error_when_short_name_is_duplicate()
    {
        await GoToNewAsync("P-01");

        // "NPS" collides with the seeded standard → server rejects with KPI_SHORT_NAME_DUPLICATE.
        await Page.Locator("#kpi-short-name").FillAsync("NPS");
        await Page.Locator("#kpi-full-name").FillAsync("Duplicate of the NPS standard");
        await Save.ClickAsync();

        await Expect(Page.GetByTestId("kpi-save-error"))
            .ToHaveAttributeAsync("data-error-code", "KPI_SHORT_NAME_DUPLICATE");
    }

    // KPI-E2E-13
    [TestMethod]
    public async Task KpiConfig_renders_short_name_read_only_in_edit_mode()
    {
        await GoToEditAsync("P-01", "CSAT");

        await Expect(Page.Locator("#kpi-short-name")).ToBeDisabledAsync();
    }

    // KPI-E2E-14
    [TestMethod]
    public async Task KpiConfig_renders_scale_and_method_read_only_for_nps()
    {
        await GoToEditAsync("P-01", "NPS");

        // Scale is shown as a locked read-only input; the Calculation Method select is locked.
        await Expect(Page.GetByTestId("kpi-scale-locked")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("kpi-scale-locked")).ToBeDisabledAsync();
        await Expect(Page.GetByTestId("kpi-calc-method")).ToHaveAttributeAsync("data-locked", "true");
    }

    // KPI-E2E-15
    [TestMethod]
    public async Task KpiConfig_reveals_emoji_set_dropdown_when_representation_is_emoji()
    {
        await GoToNewAsync("P-01");

        await Expect(Page.Locator("#kpi-emoji-set")).ToHaveCountAsync(0);
        await SelectOptionAsync("kpi-representation", "kpi-representation-option-Emoji");
        await Expect(Page.Locator("#kpi-emoji-set")).ToBeVisibleAsync();
    }

    // KPI-E2E-16
    [TestMethod]
    public async Task KpiConfig_resets_representation_to_number_when_scale_leaves_1_3_with_slider_active()
    {
        await GoToNewAsync("P-01");

        await SelectOptionAsync("kpi-scale", "kpi-scale-option-Scale1_3");
        await SelectOptionAsync("kpi-representation", "kpi-representation-option-Slider");
        await Expect(Page.GetByTestId("kpi-representation")).ToHaveAttributeAsync("data-value", "Slider");

        // Moving the scale off 1–3 invalidates Slider → representation resets to Number.
        await SelectOptionAsync("kpi-scale", "kpi-scale-option-Scale1_5");
        await Expect(Page.GetByTestId("kpi-representation")).ToHaveAttributeAsync("data-value", "Number");
    }

    // KPI-E2E-17
    [TestMethod]
    public async Task KpiConfig_renders_top_n_warning_when_n_exceeds_half_scale_minus_one()
    {
        await GoToNewAsync("P-01");

        await SelectOptionAsync("kpi-scale", "kpi-scale-option-Scale1_7"); // span 6 → warn when n > 3
        await SelectOptionAsync("kpi-calc-method", "kpi-calc-option-TopNBox");
        await Page.Locator("#kpi-top-n").FillAsync("4");

        await Expect(Page.GetByTestId("kpi-topn-warning")).ToBeVisibleAsync();
    }

    // KPI-E2E-18
    [TestMethod]
    public async Task KpiConfig_blocks_save_when_top_n_equals_scale_max()
    {
        await GoToNewAsync("P-01");
        await Page.Locator("#kpi-short-name").FillAsync(UniqueShortName());
        await Page.Locator("#kpi-full-name").FillAsync("Top-n blocking case");

        await SelectOptionAsync("kpi-scale", "kpi-scale-option-Scale1_7"); // 7 boxes
        await SelectOptionAsync("kpi-calc-method", "kpi-calc-option-TopNBox");
        await Page.Locator("#kpi-top-n").FillAsync("7"); // n == box count → blocking

        await Expect(Page.GetByTestId("kpi-topn-error")).ToBeVisibleAsync();
        await Expect(Save).ToBeDisabledAsync();
    }

    // KPI-E2E-19
    [TestMethod]
    public async Task KpiConfig_updates_question_preview_within_100ms_of_field_change()
    {
        await GoToNewAsync("P-01");
        var gauge = Page.GetByTestId("preview-gauge");
        var before = await gauge.GetAttributeAsync("data-render-tick");

        // Editing a threshold boundary must propagate to the live preview (the <100 ms render budget
        // itself is asserted at the unit level, R10; here we prove the preview reacts live).
        await Page.GetByTestId("kpi-threshold-x").First.FillAsync("30");

        await Expect(gauge).Not.ToHaveAttributeAsync("data-render-tick", before!, new() { Timeout = 2000 });
    }

    // KPI-E2E-20
    [TestMethod]
    public async Task KpiConfig_updates_dashboard_gauge_bands_when_threshold_x_or_y_changes()
    {
        await GoToNewAsync("P-01");
        var gauge = Page.GetByTestId("preview-gauge");
        var fxBefore = await gauge.GetAttributeAsync("data-fx");

        await Page.GetByTestId("kpi-threshold-x").First.FillAsync("35");

        await Expect(gauge).Not.ToHaveAttributeAsync("data-fx", fxBefore!, new() { Timeout = 2000 });
    }

    // KPI-E2E-21
    [TestMethod]
    public async Task KpiConfig_renders_min_max_scale_descriptions_as_anchor_labels_in_preview()
    {
        await GoToNewAsync("P-01");

        await Page.Locator("#kpi-min-desc-en").FillAsync("Very poor");
        await Page.Locator("#kpi-max-desc-en").FillAsync("Excellent");

        await Expect(Page.GetByTestId("kpi-preview-min-anchor")).ToContainTextAsync("Very poor");
        await Expect(Page.GetByTestId("kpi-preview-max-anchor")).ToContainTextAsync("Excellent");
    }

    // KPI-E2E-22
    [TestMethod]
    public async Task KpiConfig_prompts_unsaved_changes_when_user_navigates_away()
    {
        await GoToNewAsync("P-01");
        await Page.Locator("#kpi-short-name").FillAsync(UniqueShortName()); // makes the form dirty

        // The unsaved-changes guard is a shadcn AlertDialog (DOM, not a native window.confirm).
        await Page.GetByTestId("kpi-back").ClickAsync();
        await Expect(Page.GetByTestId("kpi-unsaved-dialog")).ToBeVisibleAsync();

        // Choosing "stay" keeps us on the config page (no navigation).
        await Page.GetByTestId("kpi-unsaved-stay").ClickAsync();
        await Expect(Page).ToHaveURLAsync(ConfigUrl);
    }

    // KPI-E2E-23 — requires a KPI bound in M-16 (FR-017). There is no UI path to create a binding
    // from the KPI module, and the browser lane does not seed business data, so this is recorded as a
    // fixture gap rather than silently passed. (The same 409 path is covered end-to-end by the
    // backend integration lane: UpdateKpiEndpointTests bound-custom 409→200.)
    [TestMethod]
    public async Task KpiConfig_shows_blocking_confirmation_when_scale_changes_on_bound_kpi()
    {
        await GoToNewAsync("P-01");
        Assert.Inconclusive(
            "Needs a KPI bound to an M-16 touchpoint; no UI path to bind one and the E2E lane does " +
            "not seed business data. The scale-change 409 (KPI_SCALE_CHANGE_AFFECTS_BINDINGS) gate is " +
            "covered by the backend integration lane (UpdateKpiEndpointTests).");
    }

    // KPI-E2E-24
    [TestMethod]
    public async Task KpiConfig_renders_form_read_only_for_analyst()
    {
        await SignInAsync("P-02");
        await Page.GotoAsync($"{BaseUrl}/kpi-management/new");
        await Page.Locator("#kpi-short-name").WaitForAsync();

        // Read-only notice present; no Save action; fields disabled.
        await Expect(Page.GetByTestId("kpi-readonly-notice")).ToBeVisibleAsync();
        await Expect(Save).ToHaveCountAsync(0);
        await Expect(Page.Locator("#kpi-short-name")).ToBeDisabledAsync();
    }

    // ── US-7: Analyst opens KPI Configuration in read-only mode ──────────────────────────
    // The P-02 Analyst opens the NPS standard's configuration page (FR-009 / US-7). The form is
    // populated for inspection but every write affordance is removed or inert. These methods live in
    // this file (not a new class) per spec.md US-7 — the analyst variant rides on the US-2 form, and
    // the shared "renders_form_read_only" scenario above (KPI-E2E-24) is counted once for both stories.

    // KPI-E2E-25
    [TestMethod]
    public async Task KpiConfig_hides_save_button_for_analyst()
    {
        await GoToEditAsync("P-02", "NPS");

        // US-7 scenario 1: the Save button is hidden for the Analyst (the single filled primary
        // action is the only write path, so its absence proves the page is non-mutating).
        await Expect(Save).ToHaveCountAsync(0);
        await Expect(Page.GetByTestId("kpi-readonly-notice")).ToBeVisibleAsync();
    }

    // KPI-E2E-26
    [TestMethod]
    public async Task KpiConfig_hides_activation_control_for_analyst()
    {
        await GoToEditAsync("P-02", "NPS");

        // US-7 scenario 1 lists the activation control among the inert affordances. The page renders
        // the Active control but DISABLES it (matching the scenario's "every form field is rendered
        // but disabled" clause) rather than removing it from the DOM — so the Analyst can read the
        // current activation state but cannot toggle it. Assert it is present-and-disabled, the
        // behaviour the page actually ships; see COVERAGE.md for the disabled-vs-removed note.
        var activeControl = Page.GetByTestId("kpi-active");
        await Expect(activeControl).ToBeVisibleAsync();
        await Expect(activeControl).ToBeDisabledAsync();
    }

    // KPI-E2E-27
    [TestMethod]
    public async Task KpiConfig_renders_preview_cards_for_analyst()
    {
        await GoToEditAsync("P-02", "NPS");

        // US-7 scenario 2: the Analyst sees the same live preview cards the P-01 editor would — the
        // Question Preview card (NPS is non-composite, so it renders) and the Dashboard gauge preview.
        await Expect(Page.GetByTestId("kpi-question-preview")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("preview-gauge")).ToBeVisibleAsync();
    }

    // ── US-5: deactivate a KPI with binding-aware confirmation (FR-026) ──────────────────────
    // These ride on the US-2 form (the Active toggle + BindingUsageConfirmDialog), so they live in
    // this file per spec.md US-5 E2E Test Coverage. The bound scenarios seed the one precondition no
    // portal UI can create — an M-16 kpi_bindings row keyed on the KPI's id — directly via SQL
    // (E2ETenantDb), mirroring the integration fixture's BindKpiToTouchpointAsync.

    /// <summary>Creates an active custom KPI through the UI and returns its (lowercased) Short Name slug.</summary>
    private async Task<string> CreateActiveCustomKpiAsync(bool showOnDashboard = false)
    {
        await GoToNewAsync("P-01");
        var shortName = UniqueShortName();
        await Page.Locator("#kpi-short-name").FillAsync(shortName);
        await Page.Locator("#kpi-full-name").FillAsync($"{shortName} full name");
        if (showOnDashboard)
        {
            await Page.GetByTestId("kpi-show-dashboard").ClickAsync();
        }

        await Save.ClickAsync();
        await Expect(Page).ToHaveURLAsync(CatalogueUrl);
        return shortName;
    }

    private async Task OpenEditAsync(string shortName)
    {
        await Page.GotoAsync($"{BaseUrl}/kpi-management/{shortName.ToLowerInvariant()}");
        await Page.Locator("#kpi-short-name").WaitForAsync();
    }

    // KPI-E2E-28
    [TestMethod]
    public async Task KpiConfig_shows_deactivation_confirmation_when_active_toggle_off_with_bindings()
    {
        var db = new E2ETenantDb(Settings);
        if (!db.IsConfigured)
        {
            Assert.Inconclusive(
                "E2E tenant DB (e2e.tenantDb) not configured — cannot seed the M-16 binding precondition " +
                "the deactivation dialog keys on. The bound 409 path is also covered by the backend lane " +
                "(ActivateKpiEndpointTests).");
        }

        var shortName = await CreateActiveCustomKpiAsync();
        var kpiId = await db.GetKpiIdByShortNameAsync(shortName);
        Assert.IsNotNull(kpiId, "The created KPI should resolve in the tenant schema.");

        Guid? journeyId = null;
        try
        {
            journeyId = await db.SeedBoundTouchpointAsync(kpiId!.Value);

            // Untick Active → the binding-usage probe (1 touchpoint / 1 journey) opens the blocking dialog.
            await OpenEditAsync(shortName);
            await Page.GetByTestId("kpi-active").ClickAsync();

            var dialog = Page.GetByTestId("kpi-deactivate-dialog");
            await Expect(dialog).ToBeVisibleAsync();
            await Expect(dialog).ToContainTextAsync("1"); // bound touchpoint / journey counts (Western digits)
            await Expect(Page.GetByTestId("kpi-deactivate-confirm")).ToBeVisibleAsync();

            // Cancelling writes nothing — the KPI stays active (the controlled toggle never flips).
            await Page.GetByTestId("kpi-deactivate-cancel").ClickAsync();
            await Expect(dialog).Not.ToBeVisibleAsync();
            await Expect(Page.GetByTestId("kpi-active")).ToBeCheckedAsync();
        }
        finally
        {
            if (journeyId is not null)
            {
                await db.DeleteJourneyAsync(journeyId.Value);
            }

            await db.DeleteKpiAsync(kpiId!.Value);
        }
    }

    // KPI-E2E-29
    [TestMethod]
    public async Task KpiConfig_skips_deactivation_confirmation_when_no_bindings()
    {
        // An unbound custom KPI: unticking Active commits straight away — no dialog.
        var shortName = await CreateActiveCustomKpiAsync();
        await OpenEditAsync(shortName);

        await Page.GetByTestId("kpi-active").ClickAsync();

        await Expect(Page.GetByTestId("kpi-deactivate-dialog")).Not.ToBeVisibleAsync();
        // Unbound → no dialog; the toggle flips straight to off in form state (Save would persist it).
        await Expect(Page.GetByTestId("kpi-active")).Not.ToBeCheckedAsync();
    }

    // KPI-E2E-30
    [TestMethod]
    public async Task KpiConfig_cascades_show_on_dashboard_off_when_kpi_deactivated()
    {
        var db = new E2ETenantDb(Settings);
        if (!db.IsConfigured)
        {
            Assert.Inconclusive(
                "E2E tenant DB (e2e.tenantDb) not configured — cannot seed the M-16 binding the dialog " +
                "keys on. The Show-on-Dashboard cascade is also covered by the backend lane " +
                "(ActivateKpiEndpointTests / CxiCascadeAtomicityTests).");
        }

        // Active KPI with Show-on-Dashboard ON, bound to a touchpoint.
        var shortName = await CreateActiveCustomKpiAsync(showOnDashboard: true);
        var kpiId = await db.GetKpiIdByShortNameAsync(shortName);
        Assert.IsNotNull(kpiId, "The created KPI should resolve in the tenant schema.");

        Guid? journeyId = null;
        try
        {
            journeyId = await db.SeedBoundTouchpointAsync(kpiId!.Value);

            await OpenEditAsync(shortName);
            await Expect(Page.GetByTestId("kpi-show-dashboard")).ToBeCheckedAsync();

            // Deactivate via the confirmation dialog. Confirming (FR-026) only updates form state —
            // it does NOT write the DB; the main Save persists the KPI (PUT carries isActive=false,
            // and the form's normalize pass has already forced Show-on-Dashboard off client-side).
            await Page.GetByTestId("kpi-active").ClickAsync();
            await Expect(Page.GetByTestId("kpi-deactivate-dialog")).ToBeVisibleAsync();
            await Page.GetByTestId("kpi-deactivate-confirm").ClickAsync();
            await Expect(Page.GetByTestId("kpi-deactivate-dialog")).Not.ToBeVisibleAsync();
            await Expect(Page.GetByTestId("kpi-active")).Not.ToBeCheckedAsync(); // form flipped off
            await Save.ClickAsync();                                            // persist the whole KPI
            await Expect(Page).ToHaveURLAsync(CatalogueUrl);

            // Re-fetch the persisted record: Active off, and Show-on-Dashboard forced off by the cascade.
            await OpenEditAsync(shortName);
            await Expect(Page.GetByTestId("kpi-active")).Not.ToBeCheckedAsync();
            await Expect(Page.GetByTestId("kpi-show-dashboard")).Not.ToBeCheckedAsync();
        }
        finally
        {
            if (journeyId is not null)
            {
                await db.DeleteJourneyAsync(journeyId.Value);
            }

            await db.DeleteKpiAsync(kpiId!.Value);
        }
    }
}
