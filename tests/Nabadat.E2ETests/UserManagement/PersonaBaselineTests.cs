using System.Text.RegularExpressions;
using Microsoft.Playwright;

using Nabadat.E2ETests.Infrastructure;

namespace Nabadat.E2ETests.UserManagement;

/// <summary>
/// US2 — Persona baseline management. Browser E2E against the running <c>frontend/</c>
/// SPA. Covers COVERAGE.md rows PB-1 and PB-2 (task T096).
///
/// The Persona Baselines page (<c>/settings/persona-baselines</c>) lists the 8 personas
/// in an accordion (<c>frontend/src/features/persona-baselines/</c>); each panel edits
/// that persona's default module grants via <c>PersonaBaselineEditor</c> (a checkbox per
/// module/mode + a confirm dialog). The route is gated by
/// <c>RequirePermission module="UserManagement"</c>, so a persona without that module
/// (P-03) gets an access-restricted state. Assertions prefer language-independent
/// signals (route, role, data-slot scoping, bilingual accessible names) — the SPA is
/// bilingual ar/en.
/// </summary>
[TestClass]
public class PersonaBaselineTests : E2ETestBase
{
    // PB-1 / T096 — a P-01 actor views the baselines, modifies a module assignment, and
    // saves (the persona flips to "Customised"). Edits P-05 — a default-deny persona no
    // fixture depends on — so the (persisted, shared) baseline change stays isolated.
    [TestMethod]
    public async Task PersonaBaseline_P01_can_view_and_modify_baseline()
    {
        await SignInAsync(); // active, MFA-enrolled P-01 from settings

        await Page.GotoAsync($"{BaseUrl}/settings/persona-baselines");
        await Expect(Page).ToHaveURLAsync(new Regex(@"/settings/persona-baselines$"));

        // Scope everything to the P-05 accordion item so assertions can't match another
        // persona's row (every panel renders the same module grid + a Customised badge).
        var p05 = Page.Locator("[data-slot='accordion-item']")
            .Filter(new() { HasTextRegex = new Regex(@"P-05") });

        // Expand P-05 and read the first module-mode checkbox's current state.
        await p05.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("P-05") }).ClickAsync();
        var firstCheckbox = p05.GetByRole(AriaRole.Checkbox).First;
        await Expect(firstCheckbox).ToBeVisibleAsync();
        var wasChecked = await firstCheckbox.IsCheckedAsync();

        // Toggle that mode → enables Save → confirm in the dialog.
        await firstCheckbox.ClickAsync();
        await p05.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("(Save baseline|حفظ الدور)") })
            .ClickAsync();

        var dialog = Page.GetByRole(AriaRole.Dialog);
        await Expect(dialog).ToBeVisibleAsync();
        await dialog.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^(Save|حفظ)$") }).ClickAsync();

        // Save succeeded: the dialog closes and the P-05 row specifically shows "Customised".
        await Expect(dialog).Not.ToBeVisibleAsync();
        await Expect(p05.GetByText(new Regex("(Customised|مُخصّص)"))).ToBeVisibleAsync();

        // The change actually persisted: reload from the server (defeating the editor's
        // in-memory draft), re-open P-05, and confirm the checkbox reflects the toggle.
        await Page.ReloadAsync();
        var p05Reloaded = Page.Locator("[data-slot='accordion-item']")
            .Filter(new() { HasTextRegex = new Regex(@"P-05") });
        await p05Reloaded.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("P-05") }).ClickAsync();
        var firstCheckboxReloaded = p05Reloaded.GetByRole(AriaRole.Checkbox).First;
        await Expect(firstCheckboxReloaded).ToBeVisibleAsync();
        if (wasChecked)
        {
            await Expect(firstCheckboxReloaded).Not.ToBeCheckedAsync();
        }
        else
        {
            await Expect(firstCheckboxReloaded).ToBeCheckedAsync();
        }
    }

    // PB-2 / T096 — a P-03 (no UserManagement module) hitting the Persona Baselines URL
    // directly gets the access-restricted state, not the editor.
    [TestMethod]
    public async Task PersonaBaseline_P03_cannot_access_page()
    {
        await SignInAsync(Settings.P03Email, Settings.P03Password, Settings.P03TotpSecret);

        await Page.GotoAsync($"{BaseUrl}/settings/persona-baselines");

        await Expect(
                Page.GetByRole(AriaRole.Heading, new() { NameRegex = new Regex("(Access restricted|الوصول مقيّد)") }))
            .ToBeVisibleAsync();
    }
}
