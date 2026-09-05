using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Nabadat.E2ETests.Infrastructure;

namespace Nabadat.E2ETests.KpiManagement;

/// <summary>
/// US-3 browser E2E coverage for the composite (CXI) variant of the KPI Configuration page
/// (<c>/kpi-management/cxi</c>), per spec.md US-3 "E2E Test Coverage" and the E2E Test Policy. Drives
/// the real MFA-gated portal as P-01 and asserts against stable <c>data-testid</c> hooks (the portal is
/// bilingual / RTL, so translated-text selectors are avoided). The seeded <c>CXI</c> standard is the
/// only composite KPI; <c>NPS</c> / <c>CSAT</c> / <c>CES</c> are seeded active non-composite members.
///
/// <para><b>Run prerequisites</b> (see COVERAGE.md): the stack must be up (Postgres + backend host +
/// <c>npm run dev</c>) with the M-06 baseline (8 seeded standards incl. CXI), and the P-01 seeded
/// credentials present in the gitignored <c>appsettings.local.json</c>.</para>
///
/// <para><b>Member-weight rows reflect the LIVE table</b> (every active non-composite KPI is a
/// candidate row driven from <c>useKpiList</c>), so the live Effective % and the Active-checkbox gate
/// are exercised client-side without persisting CXI weights. The one mutating scenario (CXI-E2E-06)
/// uses a <em>disposable</em> custom KPI it creates and then deactivates, so no shared standard is
/// touched; the disposable is left inactive (harmless residue, unique Short Name per run).</para>
/// </summary>
[TestClass]
public sealed class CxiConfigTests : E2ETestBase
{
    private static readonly Regex CatalogueUrl = new(@"/kpi-management$");
    private static readonly Regex EditUrl = new(@"/kpi-management/[a-z0-9_-]+$");

    private static string UniqueShortName() => "E2E" + Guid.NewGuid().ToString("N")[..6];

    /// <summary>Signs in and opens the seeded CXI composite's configuration page.</summary>
    private async Task GoToCxiAsync(string persona = "P-01")
    {
        await SignInAsync(persona);
        await Page.GotoAsync($"{BaseUrl}/kpi-management/cxi");
        await Page.GetByTestId("cxi-weights-table").WaitForAsync();
    }

    // CXI-E2E-01
    [TestMethod]
    public async Task CxiConfig_hides_question_preview_card()
    {
        await GoToCxiAsync();

        // CXI is computed, not surveyed — the Question Preview card is absent (FR-046); the Dashboard
        // Preview gauge still renders.
        await Expect(Page.GetByTestId("kpi-question-preview")).ToHaveCountAsync(0);
        await Expect(Page.GetByTestId("preview-gauge")).ToBeVisibleAsync();
    }

    // CXI-E2E-02
    [TestMethod]
    public async Task CxiConfig_locks_calculation_method_to_weighted_composite()
    {
        await GoToCxiAsync();

        // The composite method is shown read-only; the editable calc-method select is not rendered.
        await Expect(Page.GetByTestId("kpi-calc-composite")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("kpi-calc-composite")).ToBeDisabledAsync();
        await Expect(Page.GetByTestId("kpi-calc-method")).ToHaveCountAsync(0);
    }

    // CXI-E2E-03
    [TestMethod]
    public async Task CxiConfig_renders_weights_table_with_active_non_cxi_kpis_only()
    {
        await GoToCxiAsync();
        await Page.GetByTestId("cxi-weight-NPS").WaitForAsync();

        // Active non-composite standards appear as member rows; the CXI never lists itself.
        await Expect(Page.GetByTestId("cxi-weight-NPS")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("cxi-weight-CSAT")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("cxi-weight-CES")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("cxi-weight-CXI")).ToHaveCountAsync(0);
    }

    // CXI-E2E-04
    [TestMethod]
    public async Task CxiConfig_updates_effective_percent_live_when_weights_change()
    {
        await GoToCxiAsync();
        await Page.GetByTestId("cxi-weight-NPS").WaitForAsync();

        // NPS=3, CSAT=2, CES=1 (6 units) → 50.0 / 33.3 / 16.7, computed client-side as the user types.
        await Page.GetByTestId("cxi-weight-NPS").FillAsync("3");
        await Page.GetByTestId("cxi-weight-CSAT").FillAsync("2");
        await Page.GetByTestId("cxi-weight-CES").FillAsync("1");

        await Expect(Page.GetByTestId("cxi-effective-NPS")).ToContainTextAsync("50.0");
        await Expect(Page.GetByTestId("cxi-effective-CSAT")).ToContainTextAsync("33.3");
        await Expect(Page.GetByTestId("cxi-effective-CES")).ToContainTextAsync("16.7");
    }

    // CXI-E2E-05
    [TestMethod]
    public async Task CxiConfig_disables_active_checkbox_when_fewer_than_two_non_zero_weights()
    {
        await GoToCxiAsync();
        await Page.GetByTestId("cxi-weight-NPS").WaitForAsync();

        // Normalise to a known live state (independent of any previously-saved weights): exactly one
        // weighted member → the Active checkbox stays disabled (FR-043).
        await Page.GetByTestId("cxi-weight-CSAT").FillAsync("0");
        await Page.GetByTestId("cxi-weight-CES").FillAsync("0");
        await Page.GetByTestId("cxi-weight-NPS").FillAsync("3");
        await Expect(Page.GetByTestId("kpi-active")).ToBeDisabledAsync();

        // A second weighted member unlocks it.
        await Page.GetByTestId("cxi-weight-CSAT").FillAsync("2");
        await Expect(Page.GetByTestId("kpi-active")).ToBeEnabledAsync();
    }

    // CXI-E2E-06 — mutates state: creates a disposable custom KPI, then deactivates it from its own
    // config page; asserts it drops out of the CXI weights table (the table lists ACTIVE non-composite
    // KPIs). Uses a unique disposable so no shared standard is touched; the disposable is left inactive.
    [TestMethod]
    public async Task CxiConfig_removes_member_row_when_member_kpi_is_deactivated_elsewhere()
    {
        var name = UniqueShortName();
        await SignInAsync("P-01");

        // 1. Create an active custom KPI — a candidate CXI member.
        await Page.GotoAsync($"{BaseUrl}/kpi-management/new");
        await Page.Locator("#kpi-short-name").WaitForAsync();
        await Page.Locator("#kpi-short-name").FillAsync(name);
        await Page.Locator("#kpi-full-name").FillAsync($"{name} member");
        await Page.GetByTestId("kpi-save").ClickAsync();
        await Expect(Page).ToHaveURLAsync(CatalogueUrl);

        // 2. While active, it appears as a member row in the CXI weights table.
        await Page.GotoAsync($"{BaseUrl}/kpi-management/cxi");
        await Page.GetByTestId($"cxi-weight-{name}").WaitForAsync();
        await Expect(Page.GetByTestId($"cxi-weight-{name}")).ToBeVisibleAsync();

        // 3. Deactivate it from its own config page (i.e. "elsewhere") and save.
        await Page.GotoAsync($"{BaseUrl}/kpi-management");
        await Page.GetByTestId("kpi-table").WaitForAsync();
        await Page.Locator("#kpi-search").FillAsync(name);
        await Page.Locator($"[data-testid='kpi-row'][data-short-name='{name}']")
            .GetByRole(AriaRole.Link).First.ClickAsync();
        await Page.WaitForURLAsync(EditUrl);
        await Page.GetByTestId("kpi-active").WaitForAsync();
        await Page.GetByTestId("kpi-active").ClickAsync(); // uncheck Active
        // Toggling only updates form state, and deactivation runs an async binding-usage probe before
        // it flips the checkbox — wait for the box to clear before saving, else Save races the probe
        // and persists the KPI still active (it would stay in the CXI member list).
        await Expect(Page.GetByTestId("kpi-active")).Not.ToBeCheckedAsync();
        await Page.GetByTestId("kpi-save").ClickAsync(); // Save persists the whole KPI (isActive=false)
        await Expect(Page).ToHaveURLAsync(CatalogueUrl);

        // 4. The now-inactive KPI no longer appears among the CXI member rows.
        await Page.GotoAsync($"{BaseUrl}/kpi-management/cxi");
        await Page.GetByTestId("cxi-weight-NPS").WaitForAsync(); // list rendered
        await Expect(Page.GetByTestId($"cxi-weight-{name}")).ToHaveCountAsync(0);
    }

    // CXI-E2E-07
    [TestMethod]
    public async Task CxiConfig_renders_weight_legend_beneath_gauge()
    {
        await GoToCxiAsync();
        await Page.GetByTestId("cxi-weight-NPS").WaitForAsync();

        // Two weighted members produce a proportional legend beneath the 0–100 dashboard gauge.
        await Page.GetByTestId("cxi-weight-NPS").FillAsync("3");
        await Page.GetByTestId("cxi-weight-CSAT").FillAsync("1");

        await Expect(Page.GetByTestId("cxi-weight-legend")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("cxi-weight-legend")).ToContainTextAsync("NPS");
        await Expect(Page.GetByTestId("cxi-weight-legend")).ToContainTextAsync("75.0");
    }
}
