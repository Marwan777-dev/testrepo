using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Nabadat.E2ETests.Infrastructure;

namespace Nabadat.E2ETests.OrganizationSettings;

/// <summary>
/// US-6 browser E2E coverage for the Organization settings (spec.md US-6 "E2E Test Coverage", tasks
/// T146 / ORG-E2E-01..09), driving the real MFA-gated portal.
///
/// <para><b>Implementation divergence (recorded, not masked).</b> The spec describes a standalone
/// <c>/settings/organization</c> page; the shipped app merged Organization + Customer Journey into a
/// single unified <c>/settings</c> screen (<c>SettingsPage.tsx</c>), with the old per-section route
/// 301-redirecting to <c>/settings</c>. These tests drive <c>/settings</c> and scope to the
/// Organization section's stable hooks (<c>organization-*</c> test-ids).</para>
///
/// <para><b>Run prerequisites</b> (see COVERAGE.md): the stack must be up (Postgres + the
/// Nabadat.TenantAdmin backend host + the Vite dev server) with the M-06 baseline + organization
/// settings seeded, and the P-01 seeded credentials present in the gitignored
/// <c>appsettings.local.json</c>. Edit rights require TenantConfiguration:Manage (FR-052). NOTE: logo
/// upload + save are REAL writes against the shared E2E tenant (no rollback) — the org just ends with
/// the last-uploaded logo / name; harmless residue.</para>
/// </summary>
[TestClass]
public sealed class OrganizationSettingsTests : E2ETestBase
{
    private static readonly Regex LoginUrl = new(@"/login");

    // A 1×1 transparent PNG — a minimal, genuinely valid raster the logo endpoint accepts.
    private const string Png1x1Base64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

    /// <summary>Signs in and opens the unified Settings page, waiting for the Organization name field.</summary>
    private async Task GoToOrganizationAsync(string persona = "P-01")
    {
        await SignInAsync(persona);
        await Page.GotoAsync($"{BaseUrl}/settings");
        await Page.GetByTestId("organization-name").WaitForAsync();
    }

    // ORG-E2E-01
    [TestMethod]
    public async Task OrganizationSettings_shows_current_values_when_user_opens_section()
    {
        await GoToOrganizationAsync();

        // The section loads with the tenant's current Name + Industry + logo control rendered.
        await Expect(Page.GetByTestId("organization-name")).ToBeVisibleAsync();
        var name = await Page.GetByTestId("organization-name").InputValueAsync();
        Assert.IsFalse(string.IsNullOrWhiteSpace(name), "Organization name should load with the current value.");

        await Expect(Page.GetByTestId("organization-industry")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("organization-save")).ToBeVisibleAsync();
    }

    // ORG-E2E-02
    [TestMethod]
    public async Task OrganizationSettings_blocks_save_when_name_is_empty()
    {
        await GoToOrganizationAsync();

        await Page.GetByTestId("organization-name").FillAsync("");
        await Page.GetByTestId("organization-save").ClickAsync();

        // Client-side guard marks the field invalid and does not persist.
        await Expect(Page.GetByTestId("organization-name")).ToHaveAttributeAsync("aria-invalid", "true");
        await Expect(Page.Locator("[data-sonner-toast][data-type='success']")).ToHaveCountAsync(0);
    }

    // ORG-E2E-03 — REAL write: a valid PNG is accepted and the success toast shows.
    [TestMethod]
    public async Task OrganizationSettings_uploads_png_logo_when_file_is_valid()
    {
        await GoToOrganizationAsync();

        await Page.GetByTestId("organization-logo-input").SetInputFilesAsync(new FilePayload
        {
            Name = "logo.png",
            MimeType = "image/png",
            Buffer = Convert.FromBase64String(Png1x1Base64),
        });

        await Expect(Page.Locator("[data-sonner-toast][data-type='success']").First).ToBeVisibleAsync();
    }

    // ORG-E2E-04 — a PDF is rejected (LOGO_CONTENT_TYPE_UNSUPPORTED → error toast).
    [TestMethod]
    public async Task OrganizationSettings_rejects_pdf_logo()
    {
        await GoToOrganizationAsync();

        await Page.GetByTestId("organization-logo-input").SetInputFilesAsync(new FilePayload
        {
            Name = "logo.pdf",
            MimeType = "application/pdf",
            Buffer = Encoding.ASCII.GetBytes("%PDF-1.4\n%minimal pdf body\n"),
        });

        await Expect(Page.Locator("[data-sonner-toast][data-type='error']").First).ToBeVisibleAsync();
    }

    // ORG-E2E-05 — an SVG carrying a <script> is SANITISED server-side (was_sanitised → info toast).
    [TestMethod]
    public async Task OrganizationSettings_sanitises_svg_logo_when_payload_contains_script_or_event_handlers()
    {
        await GoToOrganizationAsync();

        const string maliciousSvg =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"16\" height=\"16\">" +
            "<script>alert(1)</script>" +
            "<rect width=\"16\" height=\"16\" fill=\"#0D8BBC\" onclick=\"alert(2)\"/></svg>";

        await Page.GetByTestId("organization-logo-input").SetInputFilesAsync(new FilePayload
        {
            Name = "logo.svg",
            MimeType = "image/svg+xml",
            Buffer = Encoding.UTF8.GetBytes(maliciousSvg),
        });

        await Expect(Page.Locator("[data-sonner-toast][data-type='info']").First).ToBeVisibleAsync();
    }

    // ORG-E2E-06 — an unparseable SVG is rejected (LOGO_SVG_UNSAFE_CONTENT → error toast). The precise
    // error code vs. ORG-E2E-04's is pinned by the backend integration lane (LogoUploadEndpointTests);
    // here we assert the user-visible rejection (error toast), which is language-independent.
    [TestMethod]
    public async Task OrganizationSettings_rejects_unparseable_svg_with_logo_svg_unsafe_content()
    {
        await GoToOrganizationAsync();

        await Page.GetByTestId("organization-logo-input").SetInputFilesAsync(new FilePayload
        {
            Name = "logo.svg",
            MimeType = "image/svg+xml",
            Buffer = Encoding.UTF8.GetBytes("this is not <<< a parseable svg document"),
        });

        await Expect(Page.Locator("[data-sonner-toast][data-type='error']").First).ToBeVisibleAsync();
    }

    // ORG-E2E-07
    [TestMethod]
    public async Task OrganizationSettings_industry_dropdown_lists_canonical_six_values()
    {
        await GoToOrganizationAsync();

        // Options come straight from the API's industry_options (single source of truth) — open the
        // Select and confirm the canonical six are listed.
        await Page.GetByTestId("organization-industry").ClickAsync();
        await Expect(Page.Locator("[data-testid^='organization-industry-option-']")).ToHaveCountAsync(6);
    }

    // ORG-E2E-08 — signed-out access to a guarded route redirects to /login.
    [TestMethod]
    public async Task OrganizationSettings_redirects_to_login_when_user_is_signed_out()
    {
        await Context.ClearCookiesAsync();
        await Page.GotoAsync($"{BaseUrl}/settings");
        await Expect(Page).ToHaveURLAsync(LoginUrl);
    }

    // ORG-E2E-09 — read-only Organization form for a persona WITHOUT edit rights. Structurally
    // unreachable with the seeded fixtures: the /settings route requires the TenantConfiguration module,
    // and every seeded persona that holds it (P-01, P-02, P-07) holds it WITH Manage, so all of them see
    // the Organization section editable (canEdit = TenantConfiguration:Manage). A persona holding
    // TenantConfiguration View-only would be needed and none is seeded. The server-side write gate
    // (FR-052) is covered by the backend integration lane (OrganizationEndpointTests). Recorded as a
    // fixture gap, not a silent pass.
    [TestMethod]
    public async Task OrganizationSettings_renders_form_read_only_for_persona_without_edit_rights()
    {
        await GoToOrganizationAsync("P-02");
        Assert.Inconclusive(
            "No seeded persona holds TenantConfiguration WITHOUT the Manage mode: the /settings route " +
            "guard requires the module, and P-01/P-02/P-07 all hold it with Manage, so the Organization " +
            "section renders editable for every viewer. A View-only TenantConfiguration fixture would be " +
            "required to exercise the read-only Organization form. The server-side write gate (FR-052) is " +
            "covered by the backend integration lane (OrganizationEndpointTests).");
    }
}
