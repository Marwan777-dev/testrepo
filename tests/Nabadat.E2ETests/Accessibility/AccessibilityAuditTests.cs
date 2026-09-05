using System.Text;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;
using Nabadat.E2ETests.Infrastructure;

namespace Nabadat.E2ETests.Accessibility;

/// <summary>
/// T153 — SC-009 accessibility audit. Runs axe-core (via <c>Deque.AxeCore.Playwright</c>) against the
/// four M-06 KPI / Settings routes (catalogue, create, NPS config, and the single unified Settings
/// page) and asserts ZERO <c>serious</c>/<c>critical</c> violations. Signs in
/// once as P-01 (which can reach every audited route) and walks the routes in one test to avoid
/// repeated MFA sign-ins (the shared fixture user's anti-replay window). A failure lists each
/// offending route → rule → impact so the fix is actionable, per the brand-voice "cause + fix" rule.
///
/// <para>Authored after the pages exist and run at the per-story checkpoint (no red checkpoint) — the
/// E2E lane exercises existing pages exactly like the integration lane. Requires the live stack
/// (Postgres + backend host + <c>npm run dev</c>), <c>E2E_BASE_URL</c>, and the Playwright browsers.</para>
/// </summary>
[TestClass]
public sealed class AccessibilityAuditTests : E2ETestBase
{
    // The four routes audited (T153). The Customer-Journey and Organization settings pages were
    // unified into one /settings screen, so there is a single Settings route here.
    // /kpi-management/:id is entered by Short Name (the controller resolves a route key as a GUID id
    // OR a case-insensitive Short Name) — NPS is always seeded.
    private static readonly (string Label, string Path)[] Routes =
    [
        ("KPI catalogue", "/kpi-management"),
        ("KPI create", "/kpi-management/new"),
        ("KPI config (NPS)", "/kpi-management/nps"),
        ("Settings landing", "/settings")
    ];

    [TestMethod]
    public async Task KpiAndSettings_routes_have_no_serious_or_critical_axe_violations_for_program_manager()
    {
        await SignInAsync("P-01");

        var failures = new StringBuilder();
        foreach (var (label, path) in Routes)
        {
            await Page.GotoAsync($"{BaseUrl}{path}");
            // Let the SPA settle (data fetch + render) before scanning the accessibility tree.
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var results = await Page.RunAxe();
            var blocking = results.Violations
                .Where(v => v.Impact is "serious" or "critical")
                .ToArray();

            foreach (var v in blocking)
            {
                var nodes = string.Join(", ", v.Nodes.Take(3).Select(n => string.Join(" ", n.Target)));
                failures.AppendLine($"  [{label}] {v.Id} ({v.Impact}) — {v.Help} | nodes: {nodes}");
            }
        }

        if (failures.Length > 0)
        {
            Assert.Fail($"axe-core found serious/critical accessibility violations (SC-009):\n{failures}");
        }
    }
}
