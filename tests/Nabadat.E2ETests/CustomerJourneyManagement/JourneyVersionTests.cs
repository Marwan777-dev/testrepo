using System.Text.RegularExpressions;
using Microsoft.Playwright;

using Nabadat.E2ETests.Infrastructure;

namespace Nabadat.E2ETests.CustomerJourneyManagement;

/// <summary>
/// M-16 US-3 (T080) — Journey Versioning. Browser E2E against the running <c>frontend/</c> SPA.
/// Covers COVERAGE.md rows PV-6…PV-8 — the journey-side half of US-3 ("Manage personas and journey
/// versioning"): publishing immutable journey versions and viewing a frozen snapshot read-only.
/// The persona-lifecycle half (PV-1…PV-5) lives in <see cref="PersonaVersionTests"/>.
///
/// Selectors/routes are taken from the journeys feature
/// (<c>frontend/src/features/journeys/</c>): a journey is created from the <c>/journeys</c> list
/// header ("New Journey" → <c>JourneyFormDialog</c>, <c>#journey-name</c> + required journey
/// type) and opened at <c>/journeys/:id/builder</c>, where "Add Stage" opens
/// <c>StageFormDialog</c> (<c>#stage-name</c>). Version History (<c>/journeys/:id/versions</c>,
/// reached from the builder header "Version History" link) offers a P-01-only "Publish New Version"
/// action and opens each frozen snapshot in a read-only Sheet (<c>VersionSnapshotViewer</c>).
///
/// Version publish (<c>VersionHistoryPage</c>) gates on <c>session.persona === "P-01"</c>; the
/// journey pages themselves are reachable by P-01 AND P-02 (<c>canAuthorJourneys</c> in
/// <c>AppLayout</c>). So the denial row (PV-7) signs in as the seeded P-02
/// (<c>e2e-p02@dev.local</c>) and asserts the privileged control is absent.
///
/// The seeded active user (<c>e2e-active@dev.local</c>) is P-01. E2E writes are real DB rows (no
/// rollback), so every journey is created with a unique name per run. The suite PINS the UI
/// language to English (<c>localStorage.i18nextLng = "en"</c>) before each flow so the version
/// labels assert deterministically against exact English text.
/// </summary>
[TestClass]
public class JourneyVersionTests : E2ETestBase
{
    private static readonly Regex BuilderUrl = new(@"/journeys/[0-9a-fA-F-]{36}/builder$");
    private static readonly Regex VersionsUrl = new(@"/journeys/[0-9a-fA-F-]{36}/versions$");

    // ── PV-6 ──────────────────────────────────────────────────────────────────────
    // A P-01 user publishes a journey version; the version history table lists the new version
    // (spec US-3 scenario: "publishes a journey version; the version history panel shows the new
    // version with a timestamp").
    [TestMethod]
    public async Task VersionHistory_P01_publishes_version_and_sees_it_listed()
    {
        await SignInAsync();
        await PinEnglishAsync();

        await CreateJourneyAndOpenBuilderAsync(Unique("E2E Publish"));
        await AddStageAsync("Awareness");

        await Page.GetByRole(AriaRole.Link, new() { Name = "Version History" }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(VersionsUrl);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Publish New Version" }).First.ClickAsync();

        // The published version appears in the history table. Match exactly: the "Version 1
        // published." success toast also contains the substring "Version 1".
        await Expect(Page.GetByText("Version 1", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByText("View snapshot").First).ToBeVisibleAsync();
    }

    // ── PV-7 ──────────────────────────────────────────────────────────────────────
    // A P-02 user can author a journey but the version history offers no "Publish New Version"
    // action (spec US-3 scenario: "P-02 cannot see or access the Publish Version action").
    [TestMethod]
    public async Task VersionHistory_P02_cannot_see_publish_action()
    {
        await SignInAsync(Settings.P02Email, Settings.P02Password, Settings.P02TotpSecret);
        await PinEnglishAsync();

        await CreateJourneyAndOpenBuilderAsync(Unique("E2E P02 Journey"));

        await Page.GetByRole(AriaRole.Link, new() { Name = "Version History" }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(VersionsUrl);
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Version History" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Publish New Version" })).ToHaveCountAsync(0);
    }

    // ── PV-8 ──────────────────────────────────────────────────────────────────────
    // Any authorized user opens the version history and views an earlier version's configuration in
    // read-only mode — the frozen snapshot Sheet shows the isSnapshot indicator and the captured
    // journey + stage (spec US-3 scenario: "open the version history panel and view an earlier
    // version's configuration in read-only mode").
    [TestMethod]
    public async Task VersionHistory_opens_published_snapshot_in_read_only_mode()
    {
        await SignInAsync();
        await PinEnglishAsync();

        var journeyName = Unique("E2E Snapshot");
        await CreateJourneyAndOpenBuilderAsync(journeyName);
        await AddStageAsync("Awareness");

        await Page.GetByRole(AriaRole.Link, new() { Name = "Version History" }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(VersionsUrl);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Publish New Version" }).First.ClickAsync();
        // The published version is listed as a row in the history table. Match exactly: the
        // "Version 1 published." success toast also contains the substring "Version 1".
        await Expect(Page.GetByText("Version 1", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByText("View snapshot").First).ToBeVisibleAsync();

        // Open the frozen snapshot — a read-only Sheet with the isSnapshot indicator.
        await Page.GetByText("View snapshot").First.ClickAsync();
        var sheet = Page.GetByRole(AriaRole.Dialog);
        await Expect(sheet).ToBeVisibleAsync();
        await Expect(sheet.GetByText("Version 1 snapshot")).ToBeVisibleAsync();
        // The frozen journey name + stage render read-only inside the snapshot.
        await Expect(sheet.GetByText(journeyName)).ToBeVisibleAsync();
        await Expect(sheet.GetByText("Awareness")).ToBeVisibleAsync();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    /// <summary>
    /// Pins the SPA UI language to English for the rest of the test. i18next (configured with the
    /// browser language detector + localStorage cache, default key <c>i18nextLng</c>) reads this on
    /// the next full load — so set it AFTER sign-in and BEFORE navigating to the feature page, which
    /// reloads the app in English. Keeps the version-label assertions deterministic.
    /// </summary>
    private Task PinEnglishAsync() =>
        Page.EvaluateAsync("() => localStorage.setItem('i18nextLng', 'en')");

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
        // Sheet never closes (so the builder, and the version history it links to, is never reached).
        await Page.Locator("#journey-type").ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = "Transactional", Exact = true }).ClickAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create journey" }).ClickAsync();
        await Expect(dialog).Not.ToBeVisibleAsync();

        // The list is newest-first, so the unique journey is on page 1 — open its builder.
        await Page.GetByRole(AriaRole.Link, new() { Name = journeyName }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(BuilderUrl);
        return Page.Url;
    }

    /// <summary>Adds a stage to the journey currently open in the builder.</summary>
    private async Task AddStageAsync(string stageName)
    {
        await Page.GetByRole(AriaRole.Button, new() { Name = "Add Stage" }).First.ClickAsync();
        var dialog = Page.GetByRole(AriaRole.Dialog);
        await Expect(dialog).ToBeVisibleAsync();
        await Page.Locator("#stage-name").FillAsync(stageName);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Add Stage" }).ClickAsync();
        await Expect(dialog).Not.ToBeVisibleAsync();
    }
}
