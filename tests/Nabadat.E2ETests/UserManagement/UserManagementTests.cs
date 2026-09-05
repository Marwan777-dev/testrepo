using System.Text.RegularExpressions;
using Microsoft.Playwright;

using Nabadat.E2ETests.Infrastructure;

namespace Nabadat.E2ETests.UserManagement;

/// <summary>
/// US2 — Permission modules, persona baselines, and user provisioning.
/// Browser E2E against the running <c>frontend/</c> SPA. Covers COVERAGE.md rows
/// USR-1 (task T093) and USR-2 (task T094).
///
/// Selectors/routes are taken from the users feature
/// (<c>frontend/src/features/users/</c>): the User Management table lives at
/// <c>/users</c> (behind <c>RequirePermission module="UserManagement"</c>); the
/// header "Invite User" action opens <c>InviteUserDialog</c> (<c>#invite-email</c> +
/// a persona Select defaulting to P-03); each username links to <c>/users/:id</c>,
/// whose <c>UserPermissionsEditor</c> renders a checkbox per module/mode and shows the
/// <c>v{n}</c> permission-version badge. Assertions prefer language-independent signals
/// (route, stable ids, role, the bilingual searchbox by accessible name) because the
/// SPA is bilingual ar/en.
/// </summary>
[TestClass]
public class UserManagementTests : E2ETestBase
{
    // USR-1 / T093 — a P-01 actor invites a new user and sees them in the list.
    // The seeded active user (e2e-active@dev.local) is P-01 with the full module set,
    // so it can reach /users and create users.
    [TestMethod]
    public async Task UserManagement_P01_can_invite_user_and_see_in_list()
    {
        await SignInAsync(); // active, MFA-enrolled P-01 from settings

        // Unique email per run — E2E writes are real DB rows (no rollback).
        var email = $"e2e-invite-{Guid.NewGuid():N}@example.com";
        await InviteUserAsync(email);

        // Filter to the new user (server-side username search) and confirm the row link.
        await SearchAsync(email);
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = email })).ToBeVisibleAsync();
    }

    // USR-2 / T094 — a P-01 actor edits a user's permission modules; the snapshot
    // version bumps. Edits a freshly-invited user so the change is isolated from the
    // signed-in account and shared fixtures.
    [TestMethod]
    public async Task UserManagement_P01_can_edit_user_permissions()
    {
        await SignInAsync();

        var email = $"e2e-perm-{Guid.NewGuid():N}@example.com";
        await InviteUserAsync(email);

        // Open the new user's detail page from its row link.
        await SearchAsync(email);
        await Page.GetByRole(AriaRole.Link, new() { Name = email }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex(@"/users/[0-9a-fA-F-]{36}$"));

        // Capture the starting permission version, toggle one module mode, save.
        var versionBadge = Page.GetByText(new Regex(@"^v\d+$"));
        await Expect(versionBadge).ToBeVisibleAsync();
        var before = await versionBadge.InnerTextAsync();

        await Page.GetByRole(AriaRole.Checkbox).First.ClickAsync(); // enables Save (dirty)
        await Page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("(Save changes|حفظ التغييرات)") })
            .ClickAsync();

        // Saved confirmation, and the permission-version badge incremented.
        await Expect(Page.GetByRole(AriaRole.Status)).ToBeVisibleAsync();
        await Expect(versionBadge).Not.ToHaveTextAsync(before);
    }

    // USR-3 / T095 — a P-07 (Tenant Administrator) sees the CX-domain module rows
    // disabled in the permissions editor, while the non-CX modules it administers stay
    // editable (FR-007: the 7 CX modules are P-01-exclusive).
    [TestMethod]
    public async Task UserManagement_P07_cannot_assign_CX_domain_modules()
    {
        await SignInAsync(Settings.P07Email, Settings.P07Password, Settings.P07TotpSecret);

        // P-07 may create users, so it can produce a target to open the editor on.
        var email = $"e2e-p07target-{Guid.NewGuid():N}@example.com";
        await InviteUserAsync(email);

        await SearchAsync(email);
        await Page.GetByRole(AriaRole.Link, new() { Name = email }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex(@"/users/[0-9a-fA-F-]{36}$"));

        // The editor renders one checkbox per (module, mode), in PERMISSION_MODULES order:
        // the first row is the CX-domain SurveyBuilder (disabled for P-07); the 8th module
        // (index 21 = UserManagement/View) is non-CX and stays editable.
        var checkboxes = Page.GetByRole(AriaRole.Checkbox);
        await Expect(checkboxes.First).ToBeVisibleAsync();
        await Expect(checkboxes.First).ToBeDisabledAsync();
        await Expect(checkboxes.Nth(21)).ToBeEnabledAsync();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Invites a user via the header dialog (default persona P-03) and waits for the dialog to close.</summary>
    private async Task InviteUserAsync(string email)
    {
        await Page.GotoAsync($"{BaseUrl}/users");
        await Expect(Page).ToHaveURLAsync(new Regex(@"/users$"));

        await Page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("(Invite User|دعوة مستخدم)") })
            .First.ClickAsync();

        var dialog = Page.GetByRole(AriaRole.Dialog);
        await Expect(dialog).ToBeVisibleAsync();
        await Page.Locator("#invite-email").FillAsync(email);
        await Page.Locator("#invite-password").FillAsync("ValidP@ss1");
        await dialog.Locator("button[type=submit]").ClickAsync();
        await Expect(dialog).Not.ToBeVisibleAsync();
    }

    /// <summary>Types into the User Management username searchbox (bilingual accessible name).</summary>
    private Task SearchAsync(string text) =>
        Page.GetByRole(AriaRole.Searchbox, new() { NameRegex = new Regex("(Search by username|البحث باسم المستخدم)") })
            .FillAsync(text);
}
