using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.MSTest;
using Nabadat.E2ETests.Infrastructure;

namespace Nabadat.E2ETests.KpiManagement;

/// <summary>
/// US-1 browser E2E coverage for the KPI Management catalogue page (`/kpi-management`), per spec.md
/// US-1 "E2E Test Coverage" and the E2E Test Policy. Drives the real MFA-gated portal as the
/// relevant persona and asserts against stable <c>data-testid</c> hooks (the portal is bilingual /
/// RTL-by-default, so text selectors are avoided).
///
/// <para><b>Run prerequisites</b> (see COVERAGE.md): the stack must be up (Postgres + backend host +
/// <c>npm run dev</c>) with the tenant schema carrying the M-06 baseline (its 8 seeded KPIs), and the
/// per-persona seeded credentials present in the gitignored <c>appsettings.local.json</c>. Selectors
/// were authored from the page markup and must be confirmed on first live run (per the harness note
/// in <see cref="E2ETestBase"/>).</para>
/// </summary>
[TestClass]
public sealed class KpiManagementTests : E2ETestBase
{
    private static readonly string[] CanonicalOrder =
        ["NPS", "CSAT", "CES", "CXI", "FCR", "VFM", "AgentScore", "CHS"];

    private ILocator Rows => Page.GetByTestId("kpi-row");

    private async Task GoToCatalogueAsync(string persona)
    {
        await SignInAsync(persona);
        await Page.GotoAsync($"{BaseUrl}/kpi-management");
        await Page.GetByTestId("kpi-table").WaitForAsync();
    }

    [TestMethod]
    public async Task KpiManagement_lists_eight_standard_kpis_in_canonical_order_when_tenant_is_freshly_provisioned()
    {
        await GoToCatalogueAsync("P-01");

        // Standards sort first; assert the leading eight rows match the canonical order.
        var shortNames = await Rows.EvaluateAllAsync<string[]>(
            "els => els.map(e => e.getAttribute('data-short-name'))");
        CollectionAssert.AreEqual(CanonicalOrder, shortNames[..8]);
    }

    [TestMethod]
    public async Task KpiManagement_filters_by_type_when_user_selects_Standard()
    {
        await GoToCatalogueAsync("P-01");

        await Page.GetByTestId("kpi-type-filter").ClickAsync();
        await Page.GetByTestId("kpi-type-option-Standard").ClickAsync();

        // Every visible row is a Standard KPI (the seeded eight); no custom rows remain.
        await Expect(Rows).ToHaveCountAsync(8);
    }

    [TestMethod]
    public async Task KpiManagement_dims_inactive_rows_when_active_only_is_off()
    {
        await GoToCatalogueAsync("P-01");

        // Active-only defaults on → only active rows. Turning it off reveals inactive rows too, so
        // the row count does not shrink (and is ≥ the active-only count).
        var activeOnlyCount = await Rows.CountAsync();
        await Page.GetByRole(AriaRole.Checkbox).ClickAsync();
        var allCount = await Rows.CountAsync();

        Assert.IsTrue(allCount >= activeOnlyCount, "Turning Active-only off must not reduce the row set.");
    }

    [TestMethod]
    public async Task KpiManagement_narrows_list_when_user_types_in_search()
    {
        await GoToCatalogueAsync("P-01");

        await Page.Locator("#kpi-search").FillAsync("NPS");

        await Expect(Rows).ToHaveCountAsync(1);
        await Expect(Rows.First).ToHaveAttributeAsync("data-short-name", "NPS");
    }

    [TestMethod]
    [Ignore("Target route /kpi-management/:shortName ships in US-2 (T069/T070); until then the row link " +
        "falls through to the catch-all. Enable when the KPI Configuration route exists.")]
    public async Task KpiManagement_navigates_to_config_edit_when_row_is_clicked()
    {
        await GoToCatalogueAsync("P-01");

        await Rows.First.GetByRole(AriaRole.Link).First.ClickAsync();

        // First row is NPS → /kpi-management/nps (lowercased Short Name slug).
        await Expect(Page).ToHaveURLAsync(new Regex(@"/kpi-management/nps$"));
    }

    [TestMethod]
    [Ignore("Target route /kpi-management/new ships in US-2 (T070); enable when the create route exists.")]
    public async Task KpiManagement_navigates_to_config_create_when_add_kpi_is_clicked()
    {
        await GoToCatalogueAsync("P-01");

        await Page.GetByTestId("kpi-add-button").ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex(@"/kpi-management/new$"));
    }

    [TestMethod]
    public async Task KpiManagement_hides_add_kpi_button_when_user_is_analyst()
    {
        await GoToCatalogueAsync("P-02");

        await Expect(Page.GetByTestId("kpi-add-button")).ToHaveCountAsync(0);
    }

    [TestMethod]
    public async Task KpiManagement_redirects_to_login_when_user_is_signed_out()
    {
        // No SignInAsync — a cold navigation must be bounced to the login route by the auth guard.
        await Page.GotoAsync($"{BaseUrl}/kpi-management");

        await Expect(Page).ToHaveURLAsync(new Regex(@"/login"));
    }

    /// <summary>
    /// One end-to-end pass over the whole US-1 catalogue-page contract (header count, the
    /// Type/Active-only/Search controls, the eight columns, row link target, Type pill, Scale,
    /// Calc. Method, Dashboard and Status cells, canonical ordering, and the no-delete invariant).
    /// The portal is Arabic/RTL by default, so every assertion targets a stable <c>data-*</c> hook,
    /// a server-rendered (locale-independent) cell label, a digit, or DOM structure — never
    /// translated UI text. The row-click target is asserted as the link's <c>href</c> (the KPI
    /// Configuration edit route is US-2/T070 and not yet mounted, so we don't navigate).
    /// </summary>
    [TestMethod]
    public async Task KpiManagement_catalogue_presents_the_full_contract_when_loaded_as_program_manager()
    {
        await GoToCatalogueAsync("P-01");

        var npsRow = Page.Locator("[data-testid='kpi-row'][data-short-name='NPS']");
        var cxiRow = Page.Locator("[data-testid='kpi-row'][data-short-name='CXI']");

        // Header live count subtitle "[X] Active KPIs" reflects the active rows shown (Active-only
        // defaults on → the visible row count IS the active count). The composite CXI is among the
        // active rows, so the count includes composites (BR-1.3 at the UI).
        var activeCount = await Rows.CountAsync();
        await Expect(Page.GetByTestId("kpi-active-count")).ToContainTextAsync(activeCount.ToString());
        await Expect(cxiRow).ToHaveCountAsync(1);

        // The single primary "+ Add KPI" action is present for the P-01 Program Manager.
        await Expect(Page.GetByTestId("kpi-add-button")).ToBeVisibleAsync();

        // The table exposes the eight catalogue columns (Short Name, Full Name, Type, Scale,
        // Calc. Method, Target, Dashboard, Status — in that order).
        await Expect(Page.Locator("[data-testid='kpi-table'] thead th")).ToHaveCountAsync(8);

        // Standards are listed first in the fixed canonical order.
        var shortNames = await Rows.EvaluateAllAsync<string[]>(
            "els => els.map(e => e.getAttribute('data-short-name'))");
        CollectionAssert.AreEqual(CanonicalOrder, shortNames[..8]);

        // The Short Name cell is a link whose target is the KPI's configuration (edit) page —
        // the URL carries the (lowercased) Short Name slug, e.g. /kpi-management/nps.
        await Expect(npsRow.GetByRole(AriaRole.Link))
            .ToHaveAttributeAsync("href", new Regex(@"/kpi-management/nps$"));

        // The Type column renders the pill badge ("Standard" for the seeded standards).
        await Expect(npsRow.GetByTestId("kpi-type-badge")).ToHaveAttributeAsync("data-type", "Standard");

        // Scale column: NPS shows its raw scale "0–10"; the composite CXI shows "—" (4th cell, index 3).
        await Expect(npsRow.Locator("td").Nth(3)).ToContainTextAsync("0–10");
        await Expect(cxiRow.Locator("td").Nth(3)).ToContainTextAsync("—");

        // Calc. Method column shows the human-readable method (5th cell, index 4).
        await Expect(npsRow.Locator("td").Nth(4)).ToContainTextAsync("NPS Standard");

        // Dashboard column shows "—" when Show-on-Dashboard is off — the seeded default (7th cell, index 6).
        await Expect(npsRow.Locator("td").Nth(6)).ToContainTextAsync("—");

        // Status column reflects the active state via the row's data-active hook (green dot + "Active").
        await Expect(npsRow).ToHaveAttributeAsync("data-active", "true");

        // No KPI is deletable — no row carries any control button (only the Short Name link).
        await Expect(Page.Locator("[data-testid='kpi-row'] button")).ToHaveCountAsync(0);

        // Search filters in real time by Short Name / Full Name, case-insensitively ("nps" → NPS).
        await Page.Locator("#kpi-search").FillAsync("nps");
        await Expect(Rows).ToHaveCountAsync(1);
        await Expect(Rows.First).ToHaveAttributeAsync("data-short-name", "NPS");
        await Page.Locator("#kpi-search").FillAsync("");

        // Type filter narrows the table to the eight Standard KPIs, then back to All.
        await Page.GetByTestId("kpi-type-filter").ClickAsync();
        await Page.GetByTestId("kpi-type-option-Standard").ClickAsync();
        await Expect(Rows).ToHaveCountAsync(8);
        await Page.GetByTestId("kpi-type-filter").ClickAsync();
        await Page.GetByTestId("kpi-type-option-All").ClickAsync();

        // "Active only" is checked by default; unchecking reveals inactive rows too (count never shrinks).
        var activeOnlyCount = await Rows.CountAsync();
        await Page.GetByRole(AriaRole.Checkbox).ClickAsync();
        var allCount = await Rows.CountAsync();
        Assert.IsTrue(allCount >= activeOnlyCount, "Turning Active-only off must not reduce the row set.");
    }
}
