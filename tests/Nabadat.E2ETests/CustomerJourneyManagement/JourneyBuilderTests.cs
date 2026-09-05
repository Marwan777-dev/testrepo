using System.Text.RegularExpressions;
using Microsoft.Playwright;

using Nabadat.E2ETests.Infrastructure;

namespace Nabadat.E2ETests.CustomerJourneyManagement;

/// <summary>
/// M-16 US-1 — Journey Builder. Browser E2E against the running <c>frontend/</c> SPA.
/// Covers COVERAGE.md rows JOUR-1 (P-01 author happy path) and JOUR-2 (a non-author
/// persona cannot reach the journey module).
///
/// Selectors/routes are taken from the journeys feature
/// (<c>frontend/src/features/journeys/</c>): the journey list lives at <c>/journeys</c>;
/// the header "New Journey" action opens <c>JourneyFormDialog</c> (<c>#journey-name</c>);
/// each journey name links to <c>/journeys/:id/builder</c>; the builder's "Add Stage" /
/// per-stage "Add Touchpoint" actions open <c>StageFormDialog</c> (<c>#stage-name</c>) and
/// <c>TouchpointFormDialog</c> (<c>#tp-name</c>); "Activate" transitions Draft→Active. The
/// "Customer Journeys" sidebar entry is gated to P-01/P-02 authors in <c>AppLayout</c>.
/// Assertions prefer language-independent signals (route, stable ids, role) because the SPA
/// is bilingual ar/en; button names are matched bilingually.
/// </summary>
[TestClass]
public class JourneyBuilderTests : E2ETestBase
{
    private static readonly Regex BuilderUrl = new(@"/journeys/[0-9a-fA-F-]{36}/builder$");

    // JOUR-1 / T041 — a P-01 author creates a journey, adds a stage and a touchpoint, and
    // transitions it Draft→Active. The seeded active user (e2e-active@dev.local) is P-01, so
    // it sees the journey nav and can author. E2E writes are real DB rows (no rollback), so the
    // journey name is unique per run.
    [TestMethod]
    public async Task JourneyBuilder_P01_creates_journey_adds_stage_and_touchpoint_and_activates()
    {
        await SignInAsync(); // active, MFA-enrolled P-01 from settings

        var journeyName = $"E2E Journey {Guid.NewGuid():N}";

        // Create the journey from the list header dialog.
        await Page.GotoAsync($"{BaseUrl}/journeys");
        await Expect(Page).ToHaveURLAsync(new Regex(@"/journeys$"));

        await Page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("(New Journey|رحلة جديدة)") })
            .First.ClickAsync();
        var createDialog = Page.GetByRole(AriaRole.Dialog);
        await Expect(createDialog).ToBeVisibleAsync();
        await Page.Locator("#journey-name").FillAsync(journeyName);
        // Journey type is a required field — without it the create form fails validation and the
        // dialog never closes. Select the first archetype (matched bilingually).
        await Page.Locator("#journey-type").ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { NameRegex = new Regex("(Transactional|معاملاتية)") })
            .ClickAsync();
        await createDialog
            .GetByRole(AriaRole.Button, new() { NameRegex = new Regex("(Create journey|إنشاء الرحلة)") })
            .ClickAsync();
        await Expect(createDialog).Not.ToBeVisibleAsync();

        // The list is newest-first, so the unique journey appears on page 1 — open its builder.
        await Page.GetByRole(AriaRole.Link, new() { Name = journeyName }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex(@"/journeys/[0-9a-fA-F-]{36}/builder$"));

        // Add a stage.
        await Page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("(Add Stage|إضافة مرحلة)") })
            .First.ClickAsync();
        var stageDialog = Page.GetByRole(AriaRole.Dialog);
        await Expect(stageDialog).ToBeVisibleAsync();
        await Page.Locator("#stage-name").FillAsync("Awareness");
        await stageDialog
            .GetByRole(AriaRole.Button, new() { NameRegex = new Regex("(Add Stage|إضافة مرحلة)") })
            .ClickAsync();
        await Expect(stageDialog).Not.ToBeVisibleAsync();

        // Add a touchpoint to that stage and confirm it renders in the column.
        await Page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("(Add Touchpoint|إضافة نقطة تماس)") })
            .First.ClickAsync();
        var tpDialog = Page.GetByRole(AriaRole.Dialog);
        await Expect(tpDialog).ToBeVisibleAsync();
        await Page.Locator("#tp-name").FillAsync("Website Visit");
        await tpDialog
            .GetByRole(AriaRole.Button, new() { NameRegex = new Regex("(Add Touchpoint|إضافة نقطة تماس)") })
            .ClickAsync();
        await Expect(tpDialog).Not.ToBeVisibleAsync();
        await Expect(Page.GetByText("Website Visit")).ToBeVisibleAsync();

        // Transition Draft → Active (activation has no confirm dialog). The Active state replaces
        // the "Activate" action with "Deactivate" — assert that, language-independently of the badge.
        await Page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("(Activate|تنشيط)") })
            .First.ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("(Deactivate|إيقاف التنشيط)") }))
            .ToBeVisibleAsync();
    }

    // JOUR-2 / T041 — a non-author persona (P-03, read-only) does not get the journey module.
    // Journey authoring is restricted to P-01/P-02 (spec persona RBAC), so the "Customer Journeys"
    // sidebar entry is persona-gated in AppLayout and must be absent for P-03. Uses the seeded
    // e2e-p03@dev.local fixture (DevDataSeeder); creds in appsettings.local.json.
    [TestMethod]
    public async Task JourneyNav_is_hidden_for_read_only_persona()
    {
        await SignInAsync(Settings.P03Email, Settings.P03Password, Settings.P03TotpSecret);

        // Signed in and on an authenticated route — the journey nav entry must not be rendered.
        await Expect(
            Page.GetByRole(AriaRole.Link, new() { NameRegex = new Regex("(Customer Journeys|رحلات العملاء)") }))
            .ToHaveCountAsync(0);
    }

    // JOUR-3…JOUR-7 exercise the builder's stage/touchpoint CRUD + validation surface. These are
    // label-heavy (Edit stage, Delete stage, Stage actions, confirm dialogs, validation copy), so —
    // like the persona/version E2E suites — they PIN the UI to English (localStorage i18nextLng)
    // for deterministic exact-label assertions rather than threading a bilingual regex through
    // every action. JOUR-1/JOUR-2 keep the bilingual-by-regex style for the cross-language signal.

    // ── JOUR-3 ──────────────────────────────────────────────────────────────────────
    // A freshly created journey has no stages — the builder shows the teaching empty state
    // ("No stages yet") with an Add-Stage call to action (CLAUDE.md empty-state rule).
    [TestMethod]
    public async Task JourneyBuilder_shows_empty_state_for_journey_with_no_stages()
    {
        await SignInAsync();
        await PinEnglishAsync();

        await CreateJourneyAndOpenBuilderAsync(Unique("E2E Empty"));

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "No stages yet" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Add Stage" }).First).ToBeVisibleAsync();
    }

    // ── JOUR-4 ──────────────────────────────────────────────────────────────────────
    // The Add-Stage form requires a name: submitting empty surfaces the inline validation error and
    // keeps the dialog open (no stage is created).
    [TestMethod]
    public async Task JourneyBuilder_shows_error_when_stage_name_is_empty()
    {
        await SignInAsync();
        await PinEnglishAsync();

        await CreateJourneyAndOpenBuilderAsync(Unique("E2E Validate"));

        await Page.GetByRole(AriaRole.Button, new() { Name = "Add Stage" }).First.ClickAsync();
        var dialog = Page.GetByRole(AriaRole.Dialog);
        await Expect(dialog).ToBeVisibleAsync();
        // Submit with an empty name → inline error, dialog stays open.
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Add Stage" }).ClickAsync();

        await Expect(dialog.GetByText("Stage name is required.")).ToBeVisibleAsync();
        await Expect(dialog).ToBeVisibleAsync();
    }

    // ── JOUR-5 ──────────────────────────────────────────────────────────────────────
    // A P-01 author renames a stage via the per-stage actions menu → Edit stage; the new name
    // replaces the old one in the accordion.
    [TestMethod]
    public async Task JourneyBuilder_P01_edits_a_stage_name()
    {
        await SignInAsync();
        await PinEnglishAsync();

        await CreateJourneyAndOpenBuilderAsync(Unique("E2E Edit Stage"));
        await AddStageAsync("Awareness");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Stage actions" }).First.ClickAsync();
        await Page.GetByRole(AriaRole.Menuitem, new() { Name = "Edit stage" }).ClickAsync();
        var dialog = Page.GetByRole(AriaRole.Dialog);
        await Expect(dialog).ToBeVisibleAsync();
        await Page.Locator("#stage-name").FillAsync("Consideration");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save stage" }).ClickAsync();
        await Expect(dialog).Not.ToBeVisibleAsync();

        await Expect(Page.GetByText("Consideration")).ToBeVisibleAsync();
        await Expect(Page.GetByText("Awareness", new() { Exact = true })).ToHaveCountAsync(0);
    }

    // ── JOUR-6 ──────────────────────────────────────────────────────────────────────
    // A P-01 author deletes a stage via the actions menu → Delete stage → confirm; the stage is
    // removed and the builder returns to the empty state.
    [TestMethod]
    public async Task JourneyBuilder_P01_deletes_a_stage()
    {
        await SignInAsync();
        await PinEnglishAsync();

        await CreateJourneyAndOpenBuilderAsync(Unique("E2E Delete Stage"));
        await AddStageAsync("Awareness");
        await Expect(Page.GetByText("Awareness", new() { Exact = true })).ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Stage actions" }).First.ClickAsync();
        await Page.GetByRole(AriaRole.Menuitem, new() { Name = "Delete stage" }).ClickAsync();
        // The menu item and the confirm button share the label "Delete stage" — scope to the dialog.
        var confirm = Page.GetByRole(AriaRole.Alertdialog);
        await Expect(confirm).ToBeVisibleAsync();
        await confirm.GetByRole(AriaRole.Button, new() { Name = "Delete stage" }).ClickAsync();

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "No stages yet" })).ToBeVisibleAsync();
    }

    // ── JOUR-7 ──────────────────────────────────────────────────────────────────────
    // A P-01 author deletes a touchpoint via its row action → confirm; the touchpoint disappears
    // from the stage.
    [TestMethod]
    public async Task JourneyBuilder_P01_deletes_a_touchpoint()
    {
        await SignInAsync();
        await PinEnglishAsync();

        await CreateJourneyAndOpenBuilderAsync(Unique("E2E Delete TP"));
        await AddStageAsync("Awareness");
        await AddTouchpointAsync("Website Visit");

        // A newly added stage stays collapsed (its touchpoint rows are clipped by the accordion's
        // overflow-hidden), so expand it before interacting with the touchpoint's delete action.
        await Page.GetByRole(AriaRole.Button, new() { Name = "Expand stage" }).First.ClickAsync();
        await Expect(Page.GetByText("Website Visit")).ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Delete touchpoint" }).First.ClickAsync();
        var confirm = Page.GetByRole(AriaRole.Alertdialog);
        await Expect(confirm).ToBeVisibleAsync();
        await confirm.GetByRole(AriaRole.Button, new() { Name = "Delete touchpoint" }).ClickAsync();

        await Expect(Page.GetByText("Website Visit")).ToHaveCountAsync(0);
    }

    // ── Helpers (English-pinned flows for JOUR-3…JOUR-7) ────────────────────────────

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    /// <summary>
    /// Pins the SPA UI language to English for the rest of the test. i18next caches the language in
    /// localStorage (key <c>i18nextLng</c>); set it AFTER sign-in and BEFORE the feature navigation,
    /// which reloads the app in English so exact-label assertions are deterministic.
    /// </summary>
    private Task PinEnglishAsync() =>
        Page.EvaluateAsync("() => localStorage.setItem('i18nextLng', 'en')");

    /// <summary>Creates a journey via the list-header dialog (name + required type) and opens its builder.</summary>
    private async Task CreateJourneyAndOpenBuilderAsync(string journeyName)
    {
        await Page.GotoAsync($"{BaseUrl}/journeys");
        await Page.GetByRole(AriaRole.Button, new() { Name = "New Journey" }).First.ClickAsync();
        var dialog = Page.GetByRole(AriaRole.Dialog);
        await Expect(dialog).ToBeVisibleAsync();
        await Page.Locator("#journey-name").FillAsync(journeyName);
        await Page.Locator("#journey-type").ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = "Transactional", Exact = true }).ClickAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create journey" }).ClickAsync();
        await Expect(dialog).Not.ToBeVisibleAsync();

        // The list is newest-first, so the unique journey is on page 1 — open its builder.
        await Page.GetByRole(AriaRole.Link, new() { Name = journeyName }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(BuilderUrl);
    }

    /// <summary>Adds a stage (name only) to the journey currently open in the builder.</summary>
    private async Task AddStageAsync(string stageName)
    {
        await Page.GetByRole(AriaRole.Button, new() { Name = "Add Stage" }).First.ClickAsync();
        var dialog = Page.GetByRole(AriaRole.Dialog);
        await Expect(dialog).ToBeVisibleAsync();
        await Page.Locator("#stage-name").FillAsync(stageName);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Add Stage" }).ClickAsync();
        await Expect(dialog).Not.ToBeVisibleAsync();
    }

    /// <summary>Adds a touchpoint (name only) to the first stage in the builder.</summary>
    private async Task AddTouchpointAsync(string tpName)
    {
        await Page.GetByRole(AriaRole.Button, new() { Name = "Add Touchpoint" }).First.ClickAsync();
        var dialog = Page.GetByRole(AriaRole.Dialog);
        await Expect(dialog).ToBeVisibleAsync();
        await Page.Locator("#tp-name").FillAsync(tpName);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Add Touchpoint" }).ClickAsync();
        await Expect(dialog).Not.ToBeVisibleAsync();
    }
}
