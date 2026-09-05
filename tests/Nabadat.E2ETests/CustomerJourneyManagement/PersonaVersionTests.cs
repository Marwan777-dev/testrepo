using System.Text.RegularExpressions;
using Microsoft.Playwright;

using Nabadat.E2ETests.Infrastructure;

namespace Nabadat.E2ETests.CustomerJourneyManagement;

/// <summary>
/// M-16 US-3 (T080) — Personas. Browser E2E against the running <c>frontend/</c> SPA. Covers
/// COVERAGE.md rows PV-1…PV-5 — the persona-lifecycle half of US-3 ("Manage personas and journey
/// versioning"): persona lifecycle, the journey persona binding selector (Active-only), and the
/// P-01-vs-P-02 authority split. The journey-versioning half (PV-6…PV-8) lives in
/// <see cref="JourneyVersionTests"/>.
///
/// Selectors/routes are taken from the journeys feature
/// (<c>frontend/src/features/journeys/</c>): the persona list lives at <c>/personas</c> — the
/// header "New Persona" action opens <c>CreatePersonaDialog</c> (<c>#persona-name-ar</c> /
/// <c>#persona-name-en</c>), each row carries a lifecycle status badge and a P-01-only per-row
/// actions menu (aria-label "Actions") whose items are status-driven (Draft→Activate,
/// Active→Deactivate) plus a destructive Archive behind an <c>AlertDialog</c>. The Journey Builder
/// (<c>/journeys/:id/builder</c>) shows a "Bound Personas" card whose "Bind Persona" dropdown lists
/// ONLY Active personas (FR-005).
///
/// Persona create/transition (<c>PersonaManagementPage</c>) gates on <c>session.persona === "P-01"</c>;
/// the journey + persona pages themselves are reachable by P-01 AND P-02 (<c>canAuthorJourneys</c>
/// in <c>AppLayout</c>). So the denial row (PV-5) signs in as the seeded P-02
/// (<c>e2e-p02@dev.local</c>, added to <c>DevDataSeeder</c> for this story) and asserts the
/// privileged controls are absent.
///
/// The seeded active user (<c>e2e-active@dev.local</c>) is P-01. E2E writes are real DB rows (no
/// rollback), so every persona/journey is created with a unique name per run. The suite PINS the
/// UI language to English (<c>localStorage.i18nextLng = "en"</c>) before each flow: the persona
/// status labels collide as Arabic substrings ("نشطة" ⊂ "غير نشطة") and the Activate/Deactivate
/// menu items collide too, so a deterministic single language lets us assert exact labels safely
/// (the bilingual ar/en rendering itself is exercised by JOUR-1/KPI-1).
/// </summary>
[TestClass]
public class PersonaVersionTests : E2ETestBase
{
    private static readonly Regex BuilderUrl = new(@"/journeys/[0-9a-fA-F-]{36}/builder$");

    // ── PV-1 ──────────────────────────────────────────────────────────────────────
    // A P-01 user creates a persona with Arabic + English labels; it appears in the list as Draft
    // (spec US-3 scenario 1: "creates a persona … and sees it in the persona list with status Draft").
    [TestMethod]
    public async Task PersonaManagement_P01_creates_persona_and_it_appears_as_draft()
    {
        await SignInAsync(); // active, MFA-enrolled P-01 from settings
        await PinEnglishAsync();
        await Page.GotoAsync($"{BaseUrl}/personas");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Personas" })).ToBeVisibleAsync();

        var nameEn = Unique("E2E Persona");
        await CreatePersonaAsync(nameEn, $"شخصية {Guid.NewGuid():N}");

        // Isolate the new persona via the search box, then assert its status is Draft.
        await Page.Locator("#persona-search").FillAsync(nameEn);
        var row = PersonaRow(nameEn);
        await Expect(row).ToBeVisibleAsync();
        await Expect(row.GetByText("Draft", new() { Exact = true })).ToBeVisibleAsync();
    }

    // ── PV-2 ──────────────────────────────────────────────────────────────────────
    // A P-01 user moves a persona through its lifecycle Draft → Active → Inactive (spec US-3
    // scenarios 1b/2/3). Each transition persists (success toast) and the status-driven actions
    // menu + badge reflect the new state.
    [TestMethod]
    public async Task PersonaManagement_P01_transitions_persona_through_lifecycle()
    {
        await SignInAsync();
        await PinEnglishAsync();
        await Page.GotoAsync($"{BaseUrl}/personas");

        var nameEn = Unique("E2E Lifecycle");
        await CreatePersonaAsync(nameEn, $"دورة {Guid.NewGuid():N}");
        await Page.Locator("#persona-search").FillAsync(nameEn);
        var row = PersonaRow(nameEn);
        await Expect(row).ToBeVisibleAsync();

        // Draft → Active.
        await OpenPersonaRowMenuAsync(nameEn);
        await Page.GetByRole(AriaRole.Menuitem, new() { Name = "Activate", Exact = true }).ClickAsync();
        await Expect(Page.GetByText("Persona status updated.")).ToBeVisibleAsync();
        await Expect(row.GetByText("Active", new() { Exact = true })).ToBeVisibleAsync();

        // Active → Inactive (the menu now offers Deactivate, proving the Active state was reached).
        await OpenPersonaRowMenuAsync(nameEn);
        await Page.GetByRole(AriaRole.Menuitem, new() { Name = "Deactivate", Exact = true }).ClickAsync();
        await Expect(row.GetByText("Inactive", new() { Exact = true })).ToBeVisibleAsync();
    }

    // ── PV-3 ──────────────────────────────────────────────────────────────────────
    // The journey "Bind Persona" selector lists ONLY Active personas (FR-005): an activated persona
    // appears, and once deactivated it disappears from the selector (spec US-3 scenarios 2 & 3).
    // NOTE: this verifies selector POPULATION (driven by GET /personas?status=Active), not binding
    // persistence — the persona↔journey binding has a known backend gap (T078), so we never bind.
    [TestMethod]
    public async Task BindingSelector_lists_active_persona_and_excludes_it_once_inactive()
    {
        await SignInAsync();
        await PinEnglishAsync();

        // Create + activate a persona.
        await Page.GotoAsync($"{BaseUrl}/personas");
        var personaEn = Unique("E2E Bindable");
        await CreatePersonaAsync(personaEn, $"قابلة {Guid.NewGuid():N}");
        await Page.Locator("#persona-search").FillAsync(personaEn);
        await OpenPersonaRowMenuAsync(personaEn);
        await Page.GetByRole(AriaRole.Menuitem, new() { Name = "Activate", Exact = true }).ClickAsync();
        await Expect(Page.GetByText("Persona status updated.")).ToBeVisibleAsync();

        // Create a journey and open its builder; the Active persona is offered in the selector.
        var builderUrl = await CreateJourneyAndOpenBuilderAsync(Unique("E2E Bind Journey"));
        await Page.GetByRole(AriaRole.Button, new() { Name = "Bind Persona" }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Menuitem, new() { Name = personaEn })).ToBeVisibleAsync();

        // Deactivate the persona, then reopen the builder — it must be gone from the selector.
        await Page.GotoAsync($"{BaseUrl}/personas");
        await Page.Locator("#persona-search").FillAsync(personaEn);
        await OpenPersonaRowMenuAsync(personaEn);
        await Page.GetByRole(AriaRole.Menuitem, new() { Name = "Deactivate", Exact = true }).ClickAsync();
        await Expect(Page.GetByText("Persona status updated.")).ToBeVisibleAsync();

        await Page.GotoAsync(builderUrl);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Bind Persona" }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Menuitem, new() { Name = personaEn })).ToHaveCountAsync(0);
    }

    // ── PV-4 ──────────────────────────────────────────────────────────────────────
    // A P-01 user archives a persona; Archived is terminal — the row drops its lifecycle actions
    // menu (no further transitions possible) (spec US-3 scenarios 1c/4).
    [TestMethod]
    public async Task PersonaManagement_P01_archives_persona_and_archive_is_terminal()
    {
        await SignInAsync();
        await PinEnglishAsync();
        await Page.GotoAsync($"{BaseUrl}/personas");

        var nameEn = Unique("E2E Archive");
        await CreatePersonaAsync(nameEn, $"أرشفة {Guid.NewGuid():N}");
        await Page.Locator("#persona-search").FillAsync(nameEn);
        var row = PersonaRow(nameEn);
        await Expect(row).ToBeVisibleAsync();

        await OpenPersonaRowMenuAsync(nameEn);
        await Page.GetByRole(AriaRole.Menuitem, new() { Name = "Archive", Exact = true }).ClickAsync();

        // Destructive confirm.
        var confirm = Page.GetByRole(AriaRole.Alertdialog);
        await Expect(confirm).ToBeVisibleAsync();
        await confirm.GetByRole(AriaRole.Button, new() { Name = "Archive", Exact = true }).ClickAsync();

        await Expect(Page.GetByText("Persona status updated.")).ToBeVisibleAsync();
        await Expect(row.GetByText("Archived", new() { Exact = true }).First).ToBeVisibleAsync();
        // Terminal: the archived row exposes no actions menu (it shows a terminal label instead).
        await Expect(row.GetByRole(AriaRole.Button, new() { Name = "Actions" })).ToHaveCountAsync(0);
    }

    // ── PV-5 ──────────────────────────────────────────────────────────────────────
    // A P-02 (CX Analyst) reaches the Personas page (read-only) but sees NO management controls —
    // no "New Persona" and no per-row actions menu (spec US-3 scenario: "P-02 cannot see persona
    // status transition controls (hidden or disabled)").
    [TestMethod]
    public async Task PersonaManagement_P02_cannot_see_management_controls()
    {
        await SignInAsync(Settings.P02Email, Settings.P02Password, Settings.P02TotpSecret);
        await PinEnglishAsync();
        await Page.GotoAsync($"{BaseUrl}/personas");

        // The page is reachable (read-only) for P-02 …
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Personas" })).ToBeVisibleAsync();
        // … but the create + transition controls are P-01 only.
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "New Persona" })).ToHaveCountAsync(0);
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Actions" })).ToHaveCountAsync(0);
    }

    // PV-6…PV-8 (journey versioning) live in JourneyVersionTests — the journey-side half of US-3.

    // ── Helpers ─────────────────────────────────────────────────────────────────────

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    /// <summary>
    /// Pins the SPA UI language to English for the rest of the test. i18next (configured with the
    /// browser language detector + localStorage cache, default key <c>i18nextLng</c>) reads this on
    /// the next full load — so set it AFTER sign-in and BEFORE navigating to the feature page, which
    /// reloads the app in English. Keeps the persona-status / Activate-Deactivate label assertions
    /// deterministic (their Arabic forms are substrings of one another).
    /// </summary>
    private Task PinEnglishAsync() =>
        Page.EvaluateAsync("() => localStorage.setItem('i18nextLng', 'en')");

    private ILocator PersonaRow(string nameEn) =>
        Page.Locator("tbody tr").Filter(new() { HasText = nameEn });

    /// <summary>Creates a persona from the /personas "New Persona" Sheet (assumes already on /personas).</summary>
    private async Task CreatePersonaAsync(string nameEn, string nameAr)
    {
        await Page.GetByRole(AriaRole.Button, new() { Name = "New Persona" }).First.ClickAsync();
        var sheet = Page.GetByRole(AriaRole.Dialog);
        await Expect(sheet).ToBeVisibleAsync();
        await Page.Locator("#persona-name-ar").FillAsync(nameAr);
        await Page.Locator("#persona-name-en").FillAsync(nameEn);
        await sheet.GetByRole(AriaRole.Button, new() { Name = "Create Persona" }).ClickAsync();
        await Expect(sheet).Not.ToBeVisibleAsync();
    }

    /// <summary>Opens the P-01-only per-row lifecycle actions menu for the named persona.</summary>
    private Task OpenPersonaRowMenuAsync(string nameEn) =>
        PersonaRow(nameEn).GetByRole(AriaRole.Button, new() { Name = "Actions" }).ClickAsync();

    /// <summary>
    /// Creates a journey via the list-header dialog and opens its builder. Returns the builder URL
    /// so a test can re-open the same journey directly after navigating away.
    /// </summary>
    private async Task<string> CreateJourneyAndOpenBuilderAsync(string journeyName)
    {
        await Page.GotoAsync($"{BaseUrl}/journeys");
        await Page.GetByRole(AriaRole.Button, new() { Name = "New Journey" }).First.ClickAsync();
        var dialog = Page.GetByRole(AriaRole.Dialog);
        await Expect(dialog).ToBeVisibleAsync();
        await Page.Locator("#journey-name").FillAsync(journeyName);
        // Journey type is a required field — without it the create form fails validation and the
        // Sheet never closes (so the builder, and the persona selector it hosts, is never reached).
        await Page.Locator("#journey-type").ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = "Transactional", Exact = true }).ClickAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create journey" }).ClickAsync();
        await Expect(dialog).Not.ToBeVisibleAsync();

        // The list is newest-first, so the unique journey is on page 1 — open its builder.
        await Page.GetByRole(AriaRole.Link, new() { Name = journeyName }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(BuilderUrl);
        return Page.Url;
    }
}
