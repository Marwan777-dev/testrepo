using System.Text.RegularExpressions;
using Microsoft.Playwright;

using Nabadat.E2ETests.Infrastructure;

namespace Nabadat.E2ETests.UserManagement;

/// <summary>
/// US1 — Tenant login with mandatory MFA, enrollment, and password reset.
/// Browser E2E against the running <c>frontend/</c> SPA. Covers COVERAGE.md rows
/// AUTH-1…AUTH-5 (tasks T064–T068).
///
/// Selectors/routes are taken from the auth pages (<c>frontend/src/features/auth/</c>):
/// login <c>#email</c>/<c>#password</c> → <c>/auth/mfa</c> (enrolled) or
/// <c>/auth/mfa/enroll</c> (first-time); the authenticated landing renders the
/// "Nabadat" heading behind the AuthGuard. Assertions prefer language-independent
/// signals (route, <c>role=alert</c>/<c>role=status</c>, the literal "Nabadat"
/// heading, sessionStorage) because the SPA is bilingual ar/en.
/// </summary>
[TestClass]
public class AuthTests : E2ETestBase
{
    // AUTH-1 / T064 — full happy path: credentials + valid TOTP create a session.
    [TestMethod]
    public async Task Login_creates_session_when_credentials_and_totp_valid()
    {
        // Always exercise the real login + MFA flow here (this IS the login test) — forceLogin
        // bypasses the cached-token fast path that the feature tests use.
        await SignInAsync(forceLogin: true); // active, MFA-enrolled user from settings

        // Landed on the authenticated shell (not /login, not /auth/*).
        await Expect(Page).ToHaveURLAsync(new Regex(@"^(?!.*/(login|auth)).*$"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Nabadat" })).ToBeVisibleAsync();

        // A session token was issued and stored.
        var token = await Page.EvaluateAsync<string?>("() => sessionStorage.getItem('session_token')");
        StringAssert.Matches(token ?? string.Empty, new Regex(@"\S"));
    }

    // AUTH-2 / T065 — a user with no MFA factor is routed to enrollment; scanning the
    // freshly issued secret and confirming the first code creates a session.
    [TestMethod]
    public async Task Login_shows_mfa_enrollment_when_user_has_no_mfa()
    {
        if (string.IsNullOrWhiteSpace(Settings.EnrolEmail))
        {
            Assert.Inconclusive("No MFA-enrollment fixture configured (E2E_ENROL_EMAIL).");
        }

        // Re-runnable: re-seed the enrollment fixture account back to pending-enrollment
        // (the seeder's enroll-user upsert, via the dev fixture endpoint) so login routes
        // to enrollment — a prior run would otherwise have permanently enrolled it.
        await ReseedEnrollUserAsync();

        // The enrollment page POSTs /mfa/enroll on mount and the response carries the
        // Base32 secret. Capture it from the wire — language-independent, and avoids
        // reading the masked secret out of the bilingual UI.
        var enrollResponse = Page.WaitForResponseAsync(r =>
            r.Url.Contains("/api/v1/auth/mfa/enroll")
            && !r.Url.EndsWith("/confirm")
            && r.Request.Method == "POST"
            && r.Ok);

        await SubmitCredentialsAsync(Settings.EnrolEmail, Settings.EnrolPassword);

        // First-time user → enrollment (QR) page, NOT the MFA challenge page.
        await Expect(Page).ToHaveURLAsync(new Regex(@"/auth/mfa/enroll"));
        await Expect(Page.GetByRole(AriaRole.Img)).ToBeVisibleAsync(); // QR code

        var response = await enrollResponse;
        var json = await response.JsonAsync();
        var secret = json!.Value.GetProperty("base32Secret").GetString()!;

        // Scan/confirm: the first valid TOTP from the issued secret completes
        // enrollment, creates the session, and lands on the authenticated shell.
        await FillOtpAsync(ComputeTotp(secret));

        await Page.WaitForURLAsync(new Regex(@"^(?!.*/(login|auth)).*$"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Nabadat" })).ToBeVisibleAsync();

        var token = await Page.EvaluateAsync<string?>("() => sessionStorage.getItem('session_token')");
        StringAssert.Matches(token ?? string.Empty, new Regex(@"\S"));
    }

    // AUTH-3 / T066 — an invalid TOTP code shows an error and does NOT sign in.
    [TestMethod]
    public async Task Login_shows_error_when_totp_code_invalid()
    {
        await SubmitCredentialsAsync(Settings.Email, Settings.Password);

        // Enrolled user → MFA challenge (not enrollment).
        await Expect(Page).ToHaveURLAsync(new Regex(@"/auth/mfa(?!/enroll)"));

        await FillOtpAsync("000000"); // onComplete fires verify("000000")

        // Error surfaced, still on the challenge page — no redirect to the shell.
        await Expect(Page.GetByRole(AriaRole.Alert)).ToBeVisibleAsync();
        await Expect(Page).ToHaveURLAsync(new Regex(@"/auth/mfa(?!/enroll)"));
    }

    // AUTH-4 / T067 — request a reset via the email form, then redeem the delivered
    // token on the token URL: setting a valid new password returns the user to /login.
    [TestMethod]
    public async Task PasswordReset_delivers_and_redeems_token()
    {
        if (string.IsNullOrWhiteSpace(Settings.ResetEmail))
        {
            Assert.Inconclusive("No password-reset fixture configured (E2E_RESET_EMAIL).");
        }

        // Request mode: the email form always shows the neutral "sent" confirmation
        // (no user enumeration). The link itself is delivered out-of-band (M-09 is
        // stubbed in Development), so the raw token is minted via the dev fixture below.
        await Page.GotoAsync($"{BaseUrl}/auth/password-reset");
        await Page.Locator("#reset-email").FillAsync(Settings.ResetEmail);
        await Page.Locator("button[type=submit]").First.ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Status)).ToBeVisibleAsync();

        // Mint a fresh single-use token (clears prior tokens + rate-limit state) and
        // redeem it on the dedicated reset account, so AUTH-1's login creds are untouched.
        var token = await IssueResetTokenAsync(Settings.ResetEmail);
        await Page.GotoAsync($"{BaseUrl}/auth/password-reset?token={Uri.EscapeDataString(token)}");

        const string newPassword = "ValidP@ss1";
        await Page.Locator("#new-password").FillAsync(newPassword);
        await Page.Locator("#confirm-password").FillAsync(newPassword);
        await Page.Locator("button[type=submit]").First.ClickAsync();

        // Redeem succeeds → the page navigates back to login to sign in with the new password.
        await Expect(Page).ToHaveURLAsync(new Regex(@"/login$"));
    }

    // AUTH-5 / T068 — the 4th reset request in the window is rate-limited.
    // Uses a unique email per run so prior-run state can't pre-exhaust the window.
    [TestMethod]
    public async Task PasswordReset_rate_limit_blocks_fourth_request()
    {
        if (string.IsNullOrWhiteSpace(Settings.ResetEmail))
        {
            Assert.Inconclusive("No password-reset fixture configured (E2E_RESET_EMAIL).");
        }

        var email = $"e2e-ratelimit-{Guid.NewGuid():N}@example.com";

        // 3 requests are allowed — each shows the neutral "sent" confirmation.
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            await Page.GotoAsync($"{BaseUrl}/auth/password-reset");
            await Page.Locator("#reset-email").FillAsync(email);
            await Page.Locator("button[type=submit]").First.ClickAsync();
            await Expect(Page.GetByRole(AriaRole.Status)).ToBeVisibleAsync();
        }

        // 4th request is rejected: a rate-limit alert shows and the form does NOT
        // flip to the "sent" confirmation.
        await Page.GotoAsync($"{BaseUrl}/auth/password-reset");
        await Page.Locator("#reset-email").FillAsync(email);
        await Page.Locator("button[type=submit]").First.ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Alert)).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Status)).Not.ToBeVisibleAsync();
    }
}
