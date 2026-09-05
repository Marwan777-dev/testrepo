using Microsoft.Playwright;
using Microsoft.Playwright.MSTest;
using Nabadat.E2ETests.Infrastructure;

namespace Nabadat.E2ETests.IntegrationHub;

/// <summary>
/// US1 browser E2E coverage for the M-13 service-channel screens — SCR-03 (`/integration-hub/
/// service-channels`) and SCR-04 (`…/new`, `…/:id`) — per spec.md US1's "E2E Test Coverage" block
/// and the E2E Test Policy. Drives the real MFA-gated SPA as the owning persona and selects on
/// stable <c>data-testid</c> / <c>id</c> hooks, never translated text (the SPA is bilingual and
/// RTL-by-default).
///
/// <para><b>Personas.</b> P-01 (CX Manager) manages the data model; P-07 (Tenant IT Admin) sees the
/// same screens read-only (BR-24). Both halves are asserted here — the read-only mirror is one of
/// US1's six required scenarios, not something deferred to US9.</para>
///
/// <para><b>Run prerequisites</b> (COVERAGE.md): the stack up (Postgres + the <c>Nabadat.TenantAdmin</c>
/// host + <c>npm run dev</c>) with the M-13 baseline applied to the e2e tenant schema (8 tables +
/// the 23 seeded built-in parameters), <c>E2E_BASE_URL</c> pointing at THIS checkout's dev server,
/// and the seeded per-persona credentials in the gitignored <c>appsettings.local.json</c>.</para>
///
/// <para><b>These tests write real rows</b> — the E2E lane has no transaction rollback, and VR-F13
/// caps a tenant at 100 service channels. Every channel a test creates or seeds is therefore torn
/// down in <see cref="CleanUpAsync"/> via <see cref="E2ETenantDb"/>, and names/IDs carry a run-unique
/// suffix so a leaked row from an earlier run can never collide with this one.</para>
/// </summary>
[TestClass]
public sealed class ServiceChannelTests : E2ETestBase
{
    private const string ChannelsRoute = "/integration-hub/service-channels";

    private E2ETenantDb Db => new(Settings);

    /// <summary>Channel ids seeded or created by the running test, removed on cleanup.</summary>
    private readonly List<Guid> _seededChannelIds = [];
    private readonly List<string> _createdChannelIds = [];

    /// <summary>Run-unique suffix; keeps every name/ID inside VR-F04's 19-char, [A-Za-z0-9-] budget.</summary>
    private static string Unique() => DateTime.UtcNow.ToString("HHmmssfff");

    [TestCleanup]
    public async Task CleanUpAsync()
    {
        if (!Db.IsConfigured)
        {
            return;
        }

        foreach (var id in _seededChannelIds)
        {
            await Db.DeleteServiceChannelAsync(id);
        }

        foreach (var channelId in _createdChannelIds)
        {
            await Db.DeleteServiceChannelByChannelIdAsync(channelId);
        }
    }

    private async Task GoToListAsync(string persona)
    {
        await SignInAsync(persona);
        await Page.GotoAsync($"{BaseUrl}{ChannelsRoute}");
        await Page.Locator("#channel-search").WaitForAsync();
    }

    private async Task GoToNewChannelAsync(string persona = "P-01")
    {
        await SignInAsync(persona);
        await Page.GotoAsync($"{BaseUrl}{ChannelsRoute}/new");
        await Page.GetByTestId("channel-id-input").WaitForAsync();
    }

    // ── AC-S4-01 ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task ServiceChannel_sanitizes_id_live_as_typed_and_caps_at_19_chars()
    {
        await GoToNewChannelAsync();

        var input = Page.GetByTestId("channel-id-input");
        // Spaces, punctuation and an underscore are all disallowed by VR-F04; the raw string is
        // also far longer than 19 characters, so both halves of the rule are exercised at once.
        await input.FillAsync("Kiosk Front!! Desk_2026 North Wing Branch");

        var value = await input.InputValueAsync();

        StringAssert.Matches(value, new System.Text.RegularExpressions.Regex("^[A-Za-z0-9-]+$"),
            $"Channel ID must keep only [A-Za-z0-9-]; got '{value}'.");
        Assert.IsTrue(value.Length <= 19, $"Channel ID must cap at 19 characters; got {value.Length} ('{value}').");
        // Case is preserved, not folded — the inbound URL matches it exactly (VR-F04).
        StringAssert.StartsWith(value, "KioskFront");
    }

    // ── AC-S4-02 ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task ServiceChannel_locks_id_field_after_first_successful_request()
    {
        if (!Db.IsConfigured)
        {
            Assert.Inconclusive(
                "Tenant DB connection not configured (e2e.tenantDb). BR-05's lock is set by a "
                + "channel's first 2xx inbound request — a US4 pipeline no console UI can trigger — "
                + "so this scenario needs the flag seeded directly.");
            return;
        }

        var suffix = Unique();
        var id = await Db.SeedServiceChannelAsync(
            nameEn: $"E2E Locked {suffix}",
            nameAr: $"قناة مقفلة {suffix}",
            channelId: $"E2E-LOCK-{suffix}",
            channelIdLocked: true);
        _seededChannelIds.Add(id);

        await SignInAsync("P-01");
        await Page.GotoAsync($"{BaseUrl}{ChannelsRoute}/{id}");

        var input = Page.GetByTestId("channel-id-input");
        await input.WaitForAsync();

        // Read-only, not disabled: the value must stay selectable/copyable (AC-S4-02).
        await Expect(input).ToHaveAttributeAsync("readonly", string.Empty);
        // The rest of the form is still editable — only the ID is locked.
        await Expect(Page.Locator("#channel-name-en")).Not.ToHaveAttributeAsync("readonly", string.Empty);
    }

    // ── AC-S4-03 ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task ServiceChannel_required_toggle_disables_when_supported_is_off()
    {
        await GoToNewChannelAsync();

        // First contract row of the 23 seeded built-ins. base-ui renders a visually-hidden native
        // input beside the real control, so select on the ARIA role, never input[type=…].
        var supported = Page.Locator("[data-testid^='supported-'][role='switch']").First;
        var required = Page.Locator("[data-testid^='required-'][role='checkbox']").First;
        await supported.WaitForAsync();

        // Off by default → Required is not offerable.
        await Expect(required).ToBeDisabledAsync();

        await supported.ClickAsync();
        await Expect(required).ToBeEnabledAsync();

        await required.ClickAsync();
        await Expect(required).ToBeCheckedAsync();

        // FR-S4-04 — clearing Supported force-clears Required in the same update.
        await supported.ClickAsync();
        await Expect(required).ToBeDisabledAsync();
        await Expect(required).Not.ToBeCheckedAsync();
    }

    // ── VR-F02 / VR-F04 ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task ServiceChannel_blocks_save_on_duplicate_name_or_id()
    {
        if (!Db.IsConfigured)
        {
            Assert.Inconclusive("Tenant DB connection not configured (e2e.tenantDb); this scenario "
                + "seeds the channel it collides with rather than creating a second one through the UI.");
            return;
        }

        var suffix = Unique();
        var takenName = $"E2E Duplicate {suffix}";
        var id = await Db.SeedServiceChannelAsync(
            nameEn: takenName,
            nameAr: $"قناة مكررة {suffix}",
            channelId: $"E2E-DUP-{suffix}");
        _seededChannelIds.Add(id);

        await GoToNewChannelAsync();

        // Same EN name (case-differing, since VR-F02 is case-insensitive) but a FREE channel ID,
        // so only the name can be what the server rejects.
        await Page.Locator("#channel-name-en").FillAsync(takenName.ToUpperInvariant());
        await Page.Locator("#channel-name-ar").FillAsync($"قناة أخرى {suffix}");
        await Page.GetByTestId("channel-id-input").FillAsync($"E2E-FREE-{suffix}");
        _createdChannelIds.Add($"E2E-FREE-{suffix}"); // in case the server ever accepts it

        await Page.GetByTestId("channel-save").ClickAsync();

        // Rejected: an inline error is attached to the EN name field and we never leave the form.
        await Expect(Page.GetByRole(AriaRole.Alert).First).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 10000 });
        await Expect(Page.GetByTestId("channel-id-input")).ToBeVisibleAsync();
        StringAssert.Contains(Page.Url, "/service-channels/new", "A duplicate name must not navigate away from the form.");
    }

    // ── BR-07 / FR-S3-02 ──────────────────────────────────────────────────────

    [TestMethod]
    public async Task ServiceChannel_list_shows_no_delete_action_anywhere()
    {
        await GoToListAsync("P-01");

        // BR-07: a channel is deactivated, never deleted. There is no DELETE endpoint and no
        // delete affordance on the list — the absence IS the enforcement, so assert it broadly
        // (testid, accessible name, and visible text) rather than on one hook.
        await Expect(Page.Locator("[data-testid*='delete' i]")).ToHaveCountAsync(0);
        await Expect(Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { NameString = "Delete" })).ToHaveCountAsync(0);
        await Expect(Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { NameString = "حذف" })).ToHaveCountAsync(0);

        // …and none on the editor either, which is the other place a delete would naturally sit.
        await GoToNewChannelAsync();
        await Expect(Page.Locator("[data-testid*='delete' i]")).ToHaveCountAsync(0);
    }

    // ── BR-24 (cross-checked with US9) ────────────────────────────────────────

    [TestMethod]
    public async Task ServiceChannel_it_admin_sees_read_only_view()
    {
        if (!Db.IsConfigured)
        {
            Assert.Inconclusive("Tenant DB connection not configured (e2e.tenantDb); the read-only "
                + "editor assertion needs a known channel to open.");
            return;
        }

        var suffix = Unique();
        var id = await Db.SeedServiceChannelAsync(
            nameEn: $"E2E ReadOnly {suffix}",
            nameAr: $"قناة للعرض {suffix}",
            channelId: $"E2E-RO-{suffix}");
        _seededChannelIds.Add(id);

        await GoToListAsync("P-07");

        // FR-GBL-05 — the screen renders, every write control is gone: no create CTA, and the row
        // action is View (an `view-*` testid), not Edit.
        await Expect(Page.GetByTestId("new-channel")).ToHaveCountAsync(0);
        await Expect(Page.GetByTestId($"view-E2E-RO-{suffix}")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId($"edit-E2E-RO-{suffix}")).ToHaveCountAsync(0);

        // The editor opens read-only rather than denying access outright (BR-24's mirror of P-01's
        // read-only integrations view): the notice is present and there is no save control.
        await Page.GotoAsync($"{BaseUrl}{ChannelsRoute}/{id}");
        await Expect(Page.GetByTestId("channel-read-only")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("channel-save")).ToHaveCountAsync(0);
        await Expect(Page.GetByTestId("access-denied")).ToHaveCountAsync(0);
    }
}
