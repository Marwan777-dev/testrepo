using System.Text.RegularExpressions;
using Microsoft.Playwright;

using Nabadat.E2ETests.Infrastructure;

namespace Nabadat.E2ETests.CustomerJourneyManagement;

/// <summary>
/// M-16 US-2 (T057) — per-touchpoint KPI bindings on a journey. Browser E2E against the running
/// <c>frontend/</c> SPA. Covers COVERAGE.md rows KPI-1 (weight-sum validation) and KPI-2 (NPS info
/// banner).
///
/// <para>Lives under <c>CustomerJourneyManagement</c> because it drives the journey KPI page at
/// <c>/journeys/:id/scoring</c> (<c>frontend/src/features/journeys/</c>), reached from the Journey
/// Builder header. Each touchpoint renders a <c>KpiWeightEditor</c>: an "Add KPI" button appends a row
/// with a KPI-type select (<c>id$='-type'</c>) + integer weight input (<c>input[id$='-weight']</c>);
/// a live sum indicator warns when weights ≠ 100% and disables the editor's "Save Changes"; selecting
/// NPS surfaces a non-blocking <c>role="note"</c> banner.</para>
///
/// <para><b>Strategic scoring config is NOT here.</b> The former per-journey scoring-model /
/// normalization editor was removed (see <c>KpiScoringPage.tsx</c> header); strategic scoring is now
/// tenant-level on Platform Settings → Customer Journey (<c>/settings</c>, feature 003). Its
/// save-and-confirmation is covered by <c>CustomerJourneySettingsTests</c> (CJS-E2E-09), not here.</para>
///
/// The seeded active user (e2e-active@dev.local) is P-01, so it can author journeys and configure
/// KPIs. E2E writes are real DB rows (no rollback), so each test creates its own journey with a unique
/// name. Assertions prefer language-independent signals (route, stable ids, role, enum keys) because
/// the SPA is bilingual ar/en; visible copy is matched bilingually.
/// </summary>
[TestClass]
public class KpiScoringTests : E2ETestBase
{
    private static readonly Regex AddKpi = new("(Add KPI|إضافة مؤشر)");
    private static readonly Regex SaveChanges = new("(Save Changes|حفظ التغييرات)");

    // KPI-1 / T057 — the editor blocks an invalid submit client-side: a single KPI weighted 50%
    // doesn't total 100%, so the live sum indicator warns and the editor's "Save Changes" is
    // disabled. (The server-side 422 weight-sum rejection is covered by integration tests
    // T053/T054; here we prove the UI never lets the bad save leave the browser.)
    [TestMethod]
    public async Task KpiEditor_disables_save_and_warns_when_weights_do_not_sum_to_100()
    {
        await SignInAsync(); // active, MFA-enrolled P-01 from settings
        await CreateJourneyWithTouchpointAndOpenScoringAsync();

        // Append a KPI row (a type is auto-selected) and give it a non-100 weight.
        await Page.GetByRole(AriaRole.Button, new() { NameRegex = AddKpi }).ClickAsync();
        await Page.Locator("input[id$='-weight']").First.FillAsync("50");

        // The live sum indicator flags the invalid total …
        await Expect(Page.GetByText(new Regex("(must sum to exactly 100|مجموع الأوزان)")))
            .ToBeVisibleAsync();
        // … and the editor's Save is disabled, so the invalid set can't be submitted.
        await Expect(Page.GetByRole(AriaRole.Button, new() { NameRegex = SaveChanges }))
            .ToBeDisabledAsync();
    }

    // KPI-2 / T057 — choosing NPS surfaces the non-blocking informational banner (role=note)
    // that mirrors the server `npsWarning`, reminding the author the survey distribution must
    // support the NPS response scale. The banner is advisory only — it never blocks the save.
    [TestMethod]
    public async Task KpiEditor_shows_nps_info_banner_when_nps_selected()
    {
        await SignInAsync();
        await CreateJourneyWithTouchpointAndOpenScoringAsync();

        await Page.GetByRole(AriaRole.Button, new() { NameRegex = AddKpi }).ClickAsync();

        // Pick NPS in the row's KPI-type select (base-ui listbox: click the trigger, then the
        // option). The option's accessible name carries the language-independent enum key "NPS".
        await Page.Locator("[id$='-type']").First.ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { NameRegex = new Regex("NPS") }).First.ClickAsync();

        var banner = Page.GetByRole(AriaRole.Note);
        await Expect(banner).ToBeVisibleAsync();
        await Expect(banner).ToContainTextAsync("NPS");
    }

    /// <summary>
    /// Creates a fresh journey (unique name — E2E writes persist), adds one stage and one
    /// touchpoint via the builder, then opens its KPI &amp; Scoring page through the header link.
    /// Mirrors the proven create flow in <see cref="JourneyBuilderTests"/>.
    /// </summary>
    private async Task CreateJourneyWithTouchpointAndOpenScoringAsync()
    {
        var journeyName = $"E2E KPI {Guid.NewGuid():N}";

        await Page.GotoAsync($"{BaseUrl}/journeys");
        await Page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("(New Journey|رحلة جديدة)") })
            .First.ClickAsync();
        var createDialog = Page.GetByRole(AriaRole.Dialog);
        await Expect(createDialog).ToBeVisibleAsync();
        await Page.Locator("#journey-name").FillAsync(journeyName);
        // Journey type is a required field — pick the first archetype (base-ui Select listbox).
        // Labels are localized, so select by option position rather than translated text.
        await Page.Locator("#journey-type").ClickAsync();
        await Page.GetByRole(AriaRole.Option).First.ClickAsync();
        await createDialog
            .GetByRole(AriaRole.Button, new() { NameRegex = new Regex("(Create journey|إنشاء الرحلة)") })
            .ClickAsync();
        await Expect(createDialog).Not.ToBeVisibleAsync();

        // The list is newest-first, so the unique journey is on page 1 — open its builder.
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

        // Add a touchpoint so the scoring page renders a KpiWeightEditor for it.
        await Page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("(Add Touchpoint|إضافة نقطة تماس)") })
            .First.ClickAsync();
        var tpDialog = Page.GetByRole(AriaRole.Dialog);
        await Expect(tpDialog).ToBeVisibleAsync();
        await Page.Locator("#tp-name").FillAsync("Website Visit");
        await tpDialog
            .GetByRole(AriaRole.Button, new() { NameRegex = new Regex("(Add Touchpoint|إضافة نقطة تماس)") })
            .ClickAsync();
        await Expect(tpDialog).Not.ToBeVisibleAsync();

        // Open KPI & Scoring from the builder header (real navigation).
        await Page.GetByRole(AriaRole.Link, new() { NameRegex = new Regex("(KPI & Scoring|المؤشرات والتقييم)") })
            .ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex(@"/journeys/[0-9a-fA-F-]{36}/scoring$"));
    }
}
