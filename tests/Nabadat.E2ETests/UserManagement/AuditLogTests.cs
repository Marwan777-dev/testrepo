using System.Text.RegularExpressions;
using Microsoft.Playwright;

using Nabadat.E2ETests.Infrastructure;

namespace Nabadat.E2ETests.UserManagement;

/// <summary>
/// US4 — Immutable audit trail. Browser E2E against the running <c>frontend/</c> SPA.
/// Covers COVERAGE.md rows AL-1..AL-4 (task T123).
///
/// The audit-log page (<c>/audit-log</c>, <c>frontend/src/features/audit-log/</c>) is
/// gated by <c>RequirePermission module="UserManagement"</c>, so only P-01/P-07 reach it
/// and a P-03 gets the access-restricted state. The page is read-only — there are no
/// edit/delete affordances. Assertions prefer language-independent signals (route, role,
/// stable ids, bilingual accessible names) — the SPA is bilingual ar/en.
///
/// Dependency note (M-17): the list/filter rows (AL-1, AL-2) read through M-17's
/// <c>IM17EventLogReader</c>, which has no production implementation until M-17 ships
/// (T127); until then <c>GET /api/v1/audit-log</c> has no data source and those two go
/// green only once the reader is wired. AL-3 (read-only) and AL-4 (access control) are
/// independent of M-17 and pass against the page shell as-is.
/// </summary>
[TestClass]
public class AuditLogTests : E2ETestBase
{
    // AL-1 / T123 — a P-01 actor opens the Audit Log and sees recent events. Seeds an
    // event first (inviting a user emits user.created) so the list is non-empty.
    [TestMethod]
    public async Task AuditLog_P01_can_view_recent_events()
    {
        await SignInAsync(); // active, MFA-enrolled P-01 from settings
        await InviteUserAsync($"e2e-audit-{Guid.NewGuid():N}@example.com");

        await GoToAuditLogAsync();

        // At least one event row is shown (event-type badge reads a dotted "<family>.<action>").
        await Expect(Page.GetByText(new Regex(@"\w+\.\w+")).First).ToBeVisibleAsync();
    }

    // AL-2 / T123 — filtering by event type narrows the list to matching rows only.
    [TestMethod]
    public async Task AuditLog_P01_can_filter_by_event_type()
    {
        await SignInAsync();
        await GoToAuditLogAsync();

        // Open the event-type select and choose permission.modified.
        await Page.Locator("#filter-event-type").ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = "permission.modified" }).ClickAsync();

        // The filter constrains the result set: no row of a different type is shown.
        await Expect(Page.GetByText("user.created")).Not.ToBeVisibleAsync();
        await Expect(Page.GetByText("session.revoked")).Not.ToBeVisibleAsync();
    }

    // AL-3 / T123 — the audit log is read-only: no edit or delete controls anywhere.
    [TestMethod]
    public async Task AuditLog_P01_cannot_edit_records()
    {
        await SignInAsync();
        await GoToAuditLogAsync();

        var editControls = Page.GetByRole(
            AriaRole.Button,
            new() { NameRegex = new Regex("(edit|delete|remove|تعديل|حذف|إزالة)", RegexOptions.IgnoreCase) });
        await Expect(editControls).ToHaveCountAsync(0);
    }

    // AL-4 / T123 — a P-03 (no UserManagement module) hitting the URL directly gets the
    // access-restricted state, not the audit log.
    [TestMethod]
    public async Task AuditLog_P03_cannot_access_page()
    {
        await SignInAsync(Settings.P03Email, Settings.P03Password, Settings.P03TotpSecret);

        await Page.GotoAsync($"{BaseUrl}/audit-log");

        await Expect(
                Page.GetByRole(AriaRole.Heading, new() { NameRegex = new Regex("(Access restricted|الوصول مقيّد)") }))
            .ToBeVisibleAsync();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Navigates to the Audit Log via its sidebar entry and waits for the route.</summary>
    private async Task GoToAuditLogAsync()
    {
        await Page.GotoAsync($"{BaseUrl}/audit-log");
        await Expect(Page).ToHaveURLAsync(new Regex(@"/audit-log$"));
        await Expect(
                Page.GetByRole(AriaRole.Heading, new() { NameRegex = new Regex("(Audit Log|سجل التدقيق)") }))
            .ToBeVisibleAsync();
    }

    /// <summary>Invites a fresh user via the User Management header dialog (default persona P-03).</summary>
    private async Task InviteUserAsync(string email)
    {
        await Page.GotoAsync($"{BaseUrl}/users");
        await Page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("(Invite User|دعوة مستخدم)") })
            .First.ClickAsync();
        var dialog = Page.GetByRole(AriaRole.Dialog);
        await Expect(dialog).ToBeVisibleAsync();
        await Page.Locator("#invite-email").FillAsync(email);
        await Page.Locator("#invite-password").FillAsync("ValidP@ss1");
        await dialog.Locator("button[type=submit]").ClickAsync();
        await Expect(dialog).Not.ToBeVisibleAsync();
    }
}
