using System.Text.RegularExpressions;
using Microsoft.Playwright;

using Nabadat.E2ETests.Infrastructure;

namespace Nabadat.E2ETests.CustomerJourneyManagement;

/// <summary>
/// M-16 US-4 (T095) — Detection Rules. Browser E2E against the running <c>frontend/</c> SPA.
/// Covers COVERAGE.md rows DET-1…DET-3 (the spec's US-4 E2E Test Coverage): a P-01/P-02 author
/// sets journey-level pain/happy thresholds and saves; sets a stage-level override that persists;
/// and sees the unmeasured-touchpoint callout for a touchpoint with no KPI bindings.
///
/// Selectors/routes are taken from the journeys feature
/// (<c>frontend/src/features/journeys/</c>): the Detection Rules page lives at
/// <c>/journeys/:id/detection</c> and is reached from the Journey Builder header's
/// "Detection Rules" link (<c>journey.openDetection</c>). It composes <c>DetectionThresholdEditor</c>
/// (T093): journey-level inputs <c>#detection-pain</c> / <c>#detection-happy</c>; a per-stage
/// override <c>Switch</c> (role=switch, aria-label "Override detection thresholds for {stage}") that
/// expands pain/happy override inputs (<c>id^='override-'</c> + <c>id$='-pain'/'-happy'</c>); a single
/// "Save detection rules" button that confirms with "Detection rules saved"; and a
/// <c>role="note"</c> callout listing every touchpoint with no KPI bindings (FR-010, excluded from
/// detection).
///
/// The seeded active user (<c>e2e-active@dev.local</c>) is P-01, so it can author journeys and
/// configure detection (P-02 may too — FR per the spec). E2E writes are real DB rows (no rollback),
/// so each test creates its own journey with a unique name. The suite PINS the UI language to
/// English (<c>localStorage.i18nextLng = "en"</c>) before navigating so the save-confirmation and
/// the override switch's aria-label can be asserted exactly (the bilingual ar/en rendering itself is
/// already exercised by JOUR-1/KPI-1). The live stack is required — authored under T095 and run green
/// at the US-4 checkpoint per the E2E Test Policy.
/// </summary>
[TestClass]
public class DetectionRulesTests : E2ETestBase
{
    private static readonly Regex DetectionUrl = new(@"/journeys/[0-9a-fA-F-]{36}/detection$");
    private const string StageName = "Awareness";
    private const string TouchpointName = "Website Visit";

    // ── DET-1 ───────────────────────────────────────────────────────────────────────
    // A P-01 author sets the journey-level pain + happy thresholds and saves; the PUT round-trips
    // and the "Detection rules saved" confirmation replaces the "not configured yet" hint
    // (spec US-4 scenario 1: sets a journey-level pain/happy threshold and saves; the configuration
    // is persisted).
    [TestMethod]
    public async Task Detection_journey_level_thresholds_save_and_show_confirmation()
    {
        await SignInAsync(); // active, MFA-enrolled P-01 from settings
        await PinEnglishAsync();
        await CreateJourneyWithStageTouchpointAndOpenDetectionAsync(Unique("E2E Detection"));

        await Page.Locator("#detection-pain").FillAsync("40");
        await Page.Locator("#detection-happy").FillAsync("75");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Save detection rules" }).ClickAsync();

        await Expect(Page.GetByText("Detection rules saved")).ToBeVisibleAsync();

        // The PUT genuinely persisted — a reload re-hydrates the saved thresholds from the server.
        await Page.ReloadAsync();
        await Expect(Page.Locator("#detection-pain")).ToHaveValueAsync("40");
        await Expect(Page.Locator("#detection-happy")).ToHaveValueAsync("75");
    }

    // ── DET-2 ───────────────────────────────────────────────────────────────────────
    // A P-01 author enables a stage-level override (35/70) on top of the journey thresholds and
    // saves; the override persists — after a reload the stage's override toggle is still on and its
    // values round-trip (spec US-4 scenario 2: sets a stage-level threshold override; the stage
    // reflects the override). The built UI represents the override as a per-stage toggle + values
    // (not a separate map "badge"), so the assertion is the override's persistence.
    [TestMethod]
    public async Task Detection_stage_override_saves_and_persists()
    {
        await SignInAsync();
        await PinEnglishAsync();
        await CreateJourneyWithStageTouchpointAndOpenDetectionAsync(Unique("E2E Override"));

        // Journey-level must be valid before the editor enables Save.
        await Page.Locator("#detection-pain").FillAsync("40");
        await Page.Locator("#detection-happy").FillAsync("75");

        // Enable the stage override and set a tighter pair (35/70 — valid: pain < happy, in range).
        // The Switch sets a unique per-stage aria-label, but base-ui auto-adds an aria-labelledby to
        // the generic visible "Override" label, which wins per the accessible-name spec — so the
        // switch's COMPUTED name is just "Override" (identical for every stage) and GetByRole(Switch,
        // Name=<per-stage>) can't disambiguate. Target the per-stage aria-label attribute directly,
        // which both uniquely identifies this stage's switch and asserts the intended label is present.
        var overrideSwitch = Page.Locator(
            $"[role=switch][aria-label='Override detection thresholds for {StageName}']");
        await overrideSwitch.ClickAsync();
        await Expect(overrideSwitch).ToBeCheckedAsync();
        await Page.Locator("input[id^='override-'][id$='-pain']").First.FillAsync("35");
        await Page.Locator("input[id^='override-'][id$='-happy']").First.FillAsync("70");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Save detection rules" }).ClickAsync();
        await Expect(Page.GetByText("Detection rules saved")).ToBeVisibleAsync();

        // Reload: the saved override re-hydrates — its toggle is on and the value round-trips.
        await Page.ReloadAsync();
        var reloadedSwitch = Page.Locator(
            $"[role=switch][aria-label='Override detection thresholds for {StageName}']");
        await Expect(reloadedSwitch).ToBeCheckedAsync();
        await Expect(Page.Locator("input[id^='override-'][id$='-pain']").First).ToHaveValueAsync("35");
    }

    // ── DET-3 ───────────────────────────────────────────────────────────────────────
    // A touchpoint with no KPI bindings is unmeasured, so the detection view surfaces the
    // "unmeasured touchpoint(s) excluded from detection" callout (role=note) listing it (spec US-4
    // scenario 3: a touchpoint with no KPIs is visually marked unmeasured and excluded from
    // detection — FR-010).
    [TestMethod]
    public async Task Detection_marks_touchpoint_without_kpis_as_unmeasured()
    {
        await SignInAsync();
        await PinEnglishAsync();
        await CreateJourneyWithStageTouchpointAndOpenDetectionAsync(Unique("E2E Unmeasured"));

        // The freshly created touchpoint has no KPI bindings → it appears in the unmeasured callout.
        var note = Page.GetByRole(AriaRole.Note);
        await Expect(note).ToBeVisibleAsync();
        await Expect(note).ToContainTextAsync("unmeasured");
        await Expect(note).ToContainTextAsync(TouchpointName);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    /// <summary>
    /// Pins the SPA UI language to English for the rest of the test. i18next (browser language
    /// detector + localStorage cache, key <c>i18nextLng</c>) reads this on the next full load — set
    /// it AFTER sign-in and BEFORE navigating to the feature page. Keeps the save-confirmation and
    /// override-switch aria-label assertions deterministic. Mirrors <see cref="PersonaVersionTests"/>.
    /// </summary>
    private Task PinEnglishAsync() =>
        Page.EvaluateAsync("() => localStorage.setItem('i18nextLng', 'en')");

    /// <summary>
    /// Creates a fresh journey (unique name — E2E writes persist), adds one stage and one
    /// touchpoint (no KPI bindings → unmeasured) via the builder, then opens its Detection Rules
    /// page through the header link. Mirrors the proven create flow in
    /// <see cref="KpiScoringTests"/>, retargeted to the detection sub-page.
    /// </summary>
    private async Task CreateJourneyWithStageTouchpointAndOpenDetectionAsync(string journeyName)
    {
        await Page.GotoAsync($"{BaseUrl}/journeys");
        await Page.GetByRole(AriaRole.Button, new() { Name = "New Journey" }).First.ClickAsync();
        var createDialog = Page.GetByRole(AriaRole.Dialog);
        await Expect(createDialog).ToBeVisibleAsync();
        await Page.Locator("#journey-name").FillAsync(journeyName);
        // Journey type is a required field — without it the create form fails validation and the
        // dialog stays open (base-ui Select listbox). The suite pins English, so pick "Transactional".
        await Page.Locator("#journey-type").ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = "Transactional", Exact = true }).ClickAsync();
        await createDialog.GetByRole(AriaRole.Button, new() { Name = "Create journey" }).ClickAsync();
        await Expect(createDialog).Not.ToBeVisibleAsync();

        // The list is newest-first, so the unique journey is on page 1 — open its builder.
        await Page.GetByRole(AriaRole.Link, new() { Name = journeyName }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex(@"/journeys/[0-9a-fA-F-]{36}/builder$"));

        // Add a stage.
        await Page.GetByRole(AriaRole.Button, new() { Name = "Add Stage" }).First.ClickAsync();
        var stageDialog = Page.GetByRole(AriaRole.Dialog);
        await Expect(stageDialog).ToBeVisibleAsync();
        await Page.Locator("#stage-name").FillAsync(StageName);
        await stageDialog.GetByRole(AriaRole.Button, new() { Name = "Add Stage" }).ClickAsync();
        await Expect(stageDialog).Not.ToBeVisibleAsync();

        // Add a touchpoint with no KPI bindings so the detection view shows the unmeasured callout.
        await Page.GetByRole(AriaRole.Button, new() { Name = "Add Touchpoint" }).First.ClickAsync();
        var tpDialog = Page.GetByRole(AriaRole.Dialog);
        await Expect(tpDialog).ToBeVisibleAsync();
        await Page.Locator("#tp-name").FillAsync(TouchpointName);
        await tpDialog.GetByRole(AriaRole.Button, new() { Name = "Add Touchpoint" }).ClickAsync();
        await Expect(tpDialog).Not.ToBeVisibleAsync();

        // Open Detection Rules from the builder header (real navigation).
        await Page.GetByRole(AriaRole.Link, new() { Name = "Detection Rules" }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(DetectionUrl);
    }
}
