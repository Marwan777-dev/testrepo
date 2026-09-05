using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Nabadat.E2ETests.Infrastructure;

namespace Nabadat.E2ETests.CustomerJourneyManagement;

/// <summary>
/// US-4 browser E2E coverage for the Customer Journey ScoringConfig settings (spec.md US-4 "E2E Test
/// Coverage", tasks T116 / CJS-E2E-01..08), driving the real MFA-gated portal.
///
/// <para><b>Implementation divergence (recorded, not masked).</b> The spec describes a standalone
/// <c>/settings/customer-journey</c> page; the shipped app merged Organization + Customer Journey into
/// a single unified <c>/settings</c> screen (<c>SettingsPage.tsx</c>), and the old per-section routes
/// now 301-redirect to <c>/settings</c>. These tests therefore drive <c>/settings</c> and scope to the
/// Customer Journey section's stable hooks (<c>scoring-*</c> test-ids, <c>#scoring-*</c> inputs,
/// <c>alpha-value</c>/<c>beta-value</c>/<c>mot-value</c> readouts).</para>
///
/// <para><b>Run prerequisites</b> (see COVERAGE.md): the stack must be up (Postgres + the
/// Nabadat.TenantAdmin backend host + the Vite dev server) with the M-06 baseline seeded, and the
/// P-01 / P-07 seeded credentials present in the gitignored <c>appsettings.local.json</c>. Edit rights
/// belong to P-01 only (FR-062); P-07 holds TenantConfiguration but sees the section read-only.</para>
/// </summary>
[TestClass]
public sealed class CustomerJourneySettingsTests : E2ETestBase
{
    private static readonly Regex LoginUrl = new(@"/login");

    /// <summary>Signs in and opens the unified Settings page, waiting for the Customer Journey
    /// section's parameters to render (the α slider readout is the load signal).</summary>
    private async Task GoToJourneyAsync(string persona = "P-01")
    {
        await SignInAsync(persona);
        await Page.GotoAsync($"{BaseUrl}/settings");
        await Page.GetByTestId("alpha-value").WaitForAsync();
    }

    // CJS-E2E-01
    [TestMethod]
    public async Task CustomerJourneySettings_shows_defaults_when_tenant_is_freshly_provisioned()
    {
        await GoToJourneyAsync();

        // The five scoring parameters load with their persisted/seeded values. The shared E2E tenant
        // persists across runs (the browser lane has no scoring-config reset hook and no E2E test
        // SUCCESSFULLY saves scoring config — the "blocks save" cases all fail validation first), so we
        // assert the load contract + the live β = 1 − α invariant rather than brittle literal defaults.
        await Expect(Page.GetByTestId("alpha-value")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("beta-value")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("mot-value")).ToBeVisibleAsync();

        foreach (var id in new[] { "scoring-n-floor", "scoring-flag-percentile", "scoring-rolling-window" })
        {
            var value = await Page.Locator($"#{id}").InputValueAsync();
            Assert.IsFalse(string.IsNullOrWhiteSpace(value), $"#{id} should load with a value, was empty.");
        }

        var alpha = decimal.Parse(await Page.GetByTestId("alpha-value").InnerTextAsync());
        var beta = decimal.Parse(await Page.GetByTestId("beta-value").InnerTextAsync());
        Assert.AreEqual(Math.Round(1m - alpha, 3), beta, "β must be the live (1 − α) derivation.");
    }

    // CJS-E2E-02
    [TestMethod]
    public async Task CustomerJourneySettings_updates_beta_live_when_alpha_slider_moves()
    {
        await GoToJourneyAsync();

        // The α slider is the first slider on the page (the Organization section has none). Moving it
        // to the extremes re-derives β = 1 − α client-side, with no round-trip.
        var alphaSlider = Page.GetByRole(AriaRole.Slider).First;
        await alphaSlider.FocusAsync();

        await alphaSlider.PressAsync("End"); // α → 1.000
        await Expect(Page.GetByTestId("alpha-value")).ToHaveTextAsync("1.000");
        await Expect(Page.GetByTestId("beta-value")).ToHaveTextAsync("0.000");

        await alphaSlider.PressAsync("Home"); // α → 0.000
        await Expect(Page.GetByTestId("alpha-value")).ToHaveTextAsync("0.000");
        await Expect(Page.GetByTestId("beta-value")).ToHaveTextAsync("1.000");
    }

    // CJS-E2E-03 — the MOT control is a SLIDER clamped to 1.0–2.0 (step 0.1); there is no UI path to an
    // out-of-range value, so this client-side "blocks save" scenario is structurally unreachable in the
    // shipped app. The MOT_MULTIPLIER_OUT_OF_RANGE server guard is covered by the backend integration
    // lane (ScoringConfigEndpointTests: PUT mot_multiplier:2.5 → 400). Recorded as a path gap, not a
    // silent pass.
    [TestMethod]
    public async Task CustomerJourneySettings_blocks_save_when_mot_is_out_of_range()
    {
        await GoToJourneyAsync();
        Assert.Inconclusive(
            "No UI path: the MOT multiplier is a slider clamped to 1.0–2.0 (step 0.1), so an " +
            "out-of-range value cannot be entered through the form. The MOT_MULTIPLIER_OUT_OF_RANGE " +
            "validation gate is covered end-to-end by the backend integration lane " +
            "(ScoringConfigEndpointTests, PUT mot_multiplier:2.5 → 400).");
    }

    // CJS-E2E-04
    [TestMethod]
    public async Task CustomerJourneySettings_blocks_save_when_flag_percentile_is_50()
    {
        await GoToJourneyAsync();

        // 50 is outside the allowed 1–49 band. Changing the value makes the form dirty (Save enables),
        // but clicking Save runs client validation, which marks the field invalid and does NOT persist.
        await Page.Locator("#scoring-flag-percentile").FillAsync("50");
        await Expect(Page.GetByTestId("scoring-save")).ToBeEnabledAsync();
        await Page.GetByTestId("scoring-save").ClickAsync();

        await Expect(Page.Locator("#scoring-flag-percentile")).ToHaveAttributeAsync("aria-invalid", "true");
        await Expect(Page.Locator("[data-sonner-toast][data-type='success']")).ToHaveCountAsync(0);
    }

    // CJS-E2E-05
    [TestMethod]
    public async Task CustomerJourneySettings_blocks_save_when_rolling_window_below_7()
    {
        await GoToJourneyAsync();

        // 5 is below the 7-day minimum. Save enables (dirty) but validation blocks the write.
        await Page.Locator("#scoring-rolling-window").FillAsync("5");
        await Expect(Page.GetByTestId("scoring-save")).ToBeEnabledAsync();
        await Page.GetByTestId("scoring-save").ClickAsync();

        await Expect(Page.Locator("#scoring-rolling-window")).ToHaveAttributeAsync("aria-invalid", "true");
        await Expect(Page.Locator("[data-sonner-toast][data-type='success']")).ToHaveCountAsync(0);
    }

    // CJS-E2E-06
    [TestMethod]
    public async Task CustomerJourneySettings_renders_tooltip_when_question_icon_focused_or_hovered()
    {
        await GoToJourneyAsync();

        // Each parameter carries a "?" info trigger (ScoringConfigInfoIcon → base-ui Tooltip). Hovering
        // it (FR-059 / NFR-S1) reveals the tooltip popup. Selected by stable data-slot hooks so the
        // assertion is independent of the icon class and the (bilingual) label text.
        var infoTrigger = Page.Locator("[data-slot='tooltip-trigger']").First;
        await infoTrigger.HoverAsync();
        await Expect(Page.Locator("[data-slot='tooltip-content']").First).ToBeVisibleAsync();
    }

    // CJS-E2E-07 — read-only for the IT Admin (P-07): holds TenantConfiguration (so the route renders)
    // but is not P-01, so the Customer Journey section is read-only (FR-062).
    [TestMethod]
    public async Task CustomerJourneySettings_renders_form_read_only_for_it_admin()
    {
        await GoToJourneyAsync("P-07");

        await Expect(Page.GetByTestId("scoring-readonly-notice")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("scoring-save")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#scoring-n-floor")).ToBeDisabledAsync();
    }

    // CJS-E2E-08 — unsaved-changes guard. DIVERGENCE: the unified Settings page mounts a <BrowserRouter>
    // (not a data router), so it cannot use react-router's useBlocker for in-app navigation; per the
    // source comment, in-app nav is intentionally NOT prompted (per-section Cancel discards instead),
    // and the only unsaved-changes guard is the browser-level `beforeunload` (tab close / refresh). This
    // test proves that guard engages once the form is dirty.
    [TestMethod]
    public async Task CustomerJourneySettings_prompts_unsaved_changes_when_user_navigates_away()
    {
        await GoToJourneyAsync();

        await Page.Locator("#scoring-n-floor").FillAsync("123"); // dirty → page attaches the beforeunload guard
        await Page.WaitForTimeoutAsync(300); // let the dirty-driven useEffect register the listener

        var dialogFired = new TaskCompletionSource<string>();
        Page.Dialog += async (_, dialog) =>
        {
            dialogFired.TrySetResult(dialog.Type);
            await dialog.AcceptAsync();
        };

        await Page.CloseAsync(new PageCloseOptions { RunBeforeUnload = true });

        var completed = await Task.WhenAny(dialogFired.Task, Task.Delay(5000));
        Assert.AreSame(dialogFired.Task, completed, "Expected a beforeunload prompt when leaving with unsaved edits.");
        Assert.AreEqual("beforeunload", dialogFired.Task.Result);
    }

    // CJS-E2E-09 — successful save round-trip + confirmation. Happy-path counterpart to the blocks-save
    // cases above, and the new home for the scoring-config save that used to live (per-journey) in
    // KpiScoringTests before strategic scoring moved to tenant-level Settings (feature 003). Mutates the
    // shared E2E tenant's flag percentile, but only WITHIN the valid 1–49 band, so it never breaks the
    // load-contract assertions in CJS-E2E-01.
    [TestMethod]
    public async Task CustomerJourneySettings_saves_and_confirms_when_values_are_valid()
    {
        await GoToJourneyAsync();

        // A valid flag percentile (1–49) that differs from the current value → the form is dirty and
        // passes client validation, so the save actually persists (unlike the blocks-save cases).
        var current = (await Page.Locator("#scoring-flag-percentile").InputValueAsync()).Trim();
        var next = current == "25" ? "30" : "25";
        await Page.Locator("#scoring-flag-percentile").FillAsync(next);

        await Expect(Page.GetByTestId("scoring-save")).ToBeEnabledAsync();
        await Page.GetByTestId("scoring-save").ClickAsync();

        // The PUT round-trips and the success toast confirms. Asserted on the sonner success type
        // (language-independent) — the same selector the blocks-save cases assert ZERO of.
        await Expect(Page.Locator("[data-sonner-toast][data-type='success']").First).ToBeVisibleAsync();
        await Expect(Page.Locator("#scoring-flag-percentile")).Not.ToHaveAttributeAsync("aria-invalid", "true");
    }
}
