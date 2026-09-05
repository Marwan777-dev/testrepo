using System.Text.RegularExpressions;
using Microsoft.Playwright;

using Nabadat.E2ETests.Infrastructure;

namespace Nabadat.E2ETests.UserManagement;

/// <summary>
/// US3 — Data scope &amp; custom rules. Browser E2E against the running <c>frontend/</c>
/// SPA. Covers COVERAGE.md rows DS-1..DS-4 (task T112).
///
/// The scope page (<c>/users/:userId/scope</c>, <c>frontend/src/features/data-scope/</c>)
/// is reached from the user detail page's "Manage data scope" link and is gated by
/// <c>RequirePermission module="UserManagement"</c>, so a non-manager persona (P-03)
/// gets the access-restricted state. Branch values are validated server-side against the
/// M-13 parameter definitions, so the branch test ingests the <c>branch</c> definition
/// up front via the (session-less) ingestion endpoint. Assertions prefer
/// language-independent signals (route, role, stable ids, bilingual accessible names) —
/// the SPA is bilingual ar/en.
///
/// API-surface notes (M-10 boundary): there is no M-10 endpoint to list/create hierarchy
/// nodes (M-11/M-13-owned), so DS-2 verifies the hierarchy-node picker is present and
/// editable rather than persisting a node id (a valid node must be seeded by M-11);
/// end-to-end hierarchy cascade is verified in the integration scenario (T111). Likewise
/// there is no data surface yet to observe scope filtering, so DS-1/DS-3 assert the
/// assignment/rule is saved and persists; enforcement is covered by the T105 unit tests.
/// </summary>
[TestClass]
public class DataScopeTests : E2ETestBase
{
    // DS-1 / T112 — a P-01 actor assigns a branch parameter scope to a user and it persists.
    [TestMethod]
    public async Task DataScope_P01_can_assign_branch_scope_and_persist()
    {
        await SignInAsync(); // active, MFA-enrolled P-01 from settings
        await IngestBranchDefinitionAsync();

        await OpenUserScopeAsync($"e2e-scope-{Guid.NewGuid():N}@example.com");

        // Add the "branch" parameter, then a permitted value. The parameter-scope card and
        // the custom-rule editor share these placeholders/labels, so scope to the first
        // match (the parameter-scope card is rendered before the custom-rules card).
        await Page.GetByPlaceholder(new Regex("(Parameter name|اسم المعامل)")).First.FillAsync("branch");
        await Page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("(Add parameter|إضافة معامل)") }).First.ClickAsync();

        await Page.GetByPlaceholder(new Regex("(Add a value|أضف قيمة)")).First.FillAsync("Riyadh");
        await Page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^(Add|إضافة)$") }).First.ClickAsync();

        await Page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("(Save scope|حفظ النطاق)") }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Status)).ToBeVisibleAsync();

        // Persisted: reload from the server and confirm the value tag is still present.
        await Page.ReloadAsync();
        await Expect(Page.GetByText("Riyadh")).ToBeVisibleAsync();
    }

    // DS-2 / T112 — the hierarchy-node picker is present and editable for an admin.
    // (Persisting a node id needs an M-11-seeded node; cascade is verified in T111.)
    [TestMethod]
    public async Task DataScope_P01_sees_hierarchy_node_picker()
    {
        await SignInAsync();
        await OpenUserScopeAsync($"e2e-hier-{Guid.NewGuid():N}@example.com");

        var nodeInput = Page.Locator("#org-node");
        await Expect(nodeInput).ToBeVisibleAsync();
        await Expect(nodeInput).ToBeEditableAsync();
    }

    // DS-3 / T112 — a P-01 actor creates a custom rule granting UpdateSurvey; it persists.
    [TestMethod]
    public async Task DataScope_P01_can_create_custom_rule()
    {
        await SignInAsync();
        await OpenUserScopeAsync($"e2e-rule-{Guid.NewGuid():N}@example.com");

        // The "add rule" draft exposes a checkbox per DOC-02 action (aria-label = action label).
        await Page.GetByRole(AriaRole.Checkbox, new() { NameRegex = new Regex("(Update survey|تحديث استبيان)") })
            .First.CheckAsync();
        await Page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("(Create rule|إنشاء قاعدة)") }).ClickAsync();

        // After creation the editor reloads; an existing rule now carries the granted action.
        await Expect(Page.GetByText(new Regex("(Rule 1|قاعدة 1)"))).ToBeVisibleAsync();
        await Expect(
                Page.GetByRole(AriaRole.Checkbox, new() { NameRegex = new Regex("(Update survey|تحديث استبيان)") }).First)
            .ToBeCheckedAsync();
    }

    // DS-4 / T112 — a P-03 (no UserManagement module) hitting the scope URL directly gets
    // the access-restricted state, not the editor.
    [TestMethod]
    public async Task DataScope_non_admin_cannot_access_scope_page()
    {
        await SignInAsync(Settings.P03Email, Settings.P03Password, Settings.P03TotpSecret);

        await Page.GotoAsync($"{BaseUrl}/users/{Guid.NewGuid()}/scope");

        await Expect(
                Page.GetByRole(AriaRole.Heading, new() { NameRegex = new Regex("(Access restricted|الوصول مقيّد)") }))
            .ToBeVisibleAsync();
    }

    // DS-5 / T112a — saving a value with no matching M-13 definition surfaces the
    // "values not allowed" alert and does not succeed (definition miss on a loaded page).
    [TestMethod]
    public async Task DataScope_shows_error_when_value_not_in_definition()
    {
        await SignInAsync();
        await OpenUserScopeAsync($"e2e-scope-invalid-{Guid.NewGuid():N}@example.com");

        // Add a parameter that was never ingested, give it a value, then save. Scope to the
        // first match — the parameter-scope card precedes the custom-rule editor, which
        // shares these placeholders/labels.
        await Page.GetByPlaceholder(new Regex("(Parameter name|اسم المعامل)")).First.FillAsync("ghost_param");
        await Page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("(Add parameter|إضافة معامل)") }).First.ClickAsync();
        await Page.GetByPlaceholder(new Regex("(Add a value|أضف قيمة)")).First.FillAsync("anything");
        await Page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^(Add|إضافة)$") }).First.ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("(Save scope|حفظ النطاق)") }).ClickAsync();

        // The page shows the validation alert; the success status does not appear.
        await Expect(Page.GetByText(new Regex("(values are not allowed|غير مسموح)"))).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Status)).Not.ToBeVisibleAsync();
    }

    // DS-6 / T112a — direct-navigating to a scope URL for a user that doesn't exist
    // (target miss on the page's getUserScope) shows the load-error state, not the editor.
    [TestMethod]
    public async Task DataScope_shows_load_error_for_unknown_user()
    {
        await SignInAsync();

        await Page.GotoAsync($"{BaseUrl}/users/{Guid.NewGuid()}/scope");

        await Expect(Page.GetByText(new Regex("(Couldn't load the user's scope|تعذّر تحميل نطاق المستخدم)")))
            .ToBeVisibleAsync();
        // The editor (hierarchy-node input) is not rendered in the error state.
        await Expect(Page.Locator("#org-node")).Not.ToBeVisibleAsync();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Ingests the <c>branch</c> M-13 parameter definition (session-less internal endpoint).</summary>
    private async Task IngestBranchDefinitionAsync()
    {
        var response = await Context.APIRequest.PostAsync($"{BaseUrl}/api/v1/authorization/scope/parameters", new()
        {
            DataObject = new
            {
                sourceModule = "M-13",
                parameters = new[]
                {
                    new { name = "branch", label = "Branch", allowedValues = new[] { "Riyadh", "Jeddah", "Dammam" } },
                },
            },
        });
        Assert.IsTrue(response.Ok, $"parameter ingestion failed ({response.Status}).");
    }

    /// <summary>Invites a user, opens their detail page, and follows the "Manage data scope" link.</summary>
    private async Task OpenUserScopeAsync(string email)
    {
        // Invite a fresh target via the User Management header dialog (default persona P-03).
        await Page.GotoAsync($"{BaseUrl}/users");
        await Page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("(Invite User|دعوة مستخدم)") })
            .First.ClickAsync();
        var dialog = Page.GetByRole(AriaRole.Dialog);
        await Expect(dialog).ToBeVisibleAsync();
        await Page.Locator("#invite-email").FillAsync(email);
        await Page.Locator("#invite-password").FillAsync("ValidP@ss1");
        await dialog.Locator("button[type=submit]").ClickAsync();
        await Expect(dialog).Not.ToBeVisibleAsync();

        // Open the new user's detail page, then its scope page.
        await Page.GetByRole(AriaRole.Searchbox, new() { NameRegex = new Regex("(Search by username|البحث باسم المستخدم)") })
            .FillAsync(email);
        await Page.GetByRole(AriaRole.Link, new() { Name = email }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex(@"/users/[0-9a-fA-F-]{36}$"));

        await Page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("(Manage data scope|إدارة نطاق البيانات)") })
            .ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex(@"/users/[0-9a-fA-F-]{36}/scope$"));
    }
}
