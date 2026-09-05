using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.MSTest;
using OtpNet;

namespace Nabadat.E2ETests.Infrastructure;

/// <summary>
/// Base class for every browser E2E test in the (single) `frontend/` SPA. Extends Playwright's
/// MSTest <see cref="PageTest"/> to add:
/// <list type="bullet">
///   <item>real, MFA-gated sign-in — by persona (<see cref="SignInAsync(string)"/>), by explicit
///   credentials (<see cref="SignInAsync(string,string,string)"/>), or via the active seeded user
///   (<see cref="SignInAsync()"/>) — landing an opaque session token in
///   <c>sessionStorage.session_token</c> (the app's <c>features/auth/session-token.ts</c> key);</item>
///   <item>per-test Playwright trace + screenshot attached to the MSTest result (VS Test Explorer
///   "Attachments");</item>
///   <item>credentials bound from the gitignored <c>appsettings.local.json</c>;</item>
///   <item>dev-fixture helpers (<see cref="ReseedEnrollUserAsync"/>, <see cref="IssueResetTokenAsync"/>)
///   for the re-runnable auth flows.</item>
/// </list>
/// The SPA must be running and reachable at <see cref="E2ESettings.BaseUrl"/> (set via
/// <c>E2E_BASE_URL</c>); Playwright browsers must be installed (<c>playwright.ps1 install</c>).
///
/// <para>There is one harness because the repo ships one frontend app. If a second, separately
/// authenticated SPA is ever added, give it its own <c>Infrastructure/&lt;App&gt;/</c> base class
/// rather than branching this one.</para>
/// </summary>
public abstract class E2ETestBase : PageTest
{
    internal static E2ESettings Config { get; } = E2ESettings.Load();

    protected E2ESettings Settings => Config;

    protected string BaseUrl => Settings.BaseUrl.TrimEnd('/');

    private static string ArtifactsDir => Path.Combine(AppContext.BaseDirectory, "playwright-artifacts");

    [TestInitialize]
    public async Task StartTracingAsync()
    {
        await Context.Tracing.StartAsync(new TracingStartOptions
        {
            Title = TestContext.TestName,
            Screenshots = true,
            Snapshots = true,
            Sources = true,
        });
    }

    [TestCleanup]
    public async Task CaptureArtifactsAsync()
    {
        Directory.CreateDirectory(ArtifactsDir);
        var safeName = Sanitize(TestContext.TestName ?? "test");

        var screenshotPath = Path.Combine(ArtifactsDir, $"{safeName}.png");
        try
        {
            await Page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = true });
            TestContext.AddResultFile(screenshotPath);
        }
        catch { /* page may be closed on hard failures */ }

        var tracePath = Path.Combine(ArtifactsDir, $"{safeName}.zip");
        try
        {
            await Context.Tracing.StopAsync(new TracingStopOptions { Path = tracePath });
            TestContext.AddResultFile(tracePath);
        }
        catch { /* tracing may not have started */ }

        // Double-clickable launcher that opens this test's trace in the viewer.
        if (File.Exists(tracePath))
        {
            TestContext.AddResultFile(WriteTraceLauncher(safeName, Path.GetFileName(tracePath)));
        }
    }

    private static string WriteTraceLauncher(string name, string traceFileName)
    {
        var safe = traceFileName.Replace("%", "%%"); // '%' is the only batch metachar a sanitized name can hold
        var cmd = string.Join("\r\n",
            "@echo off", "setlocal",
            "set \"HERE=%~dp0\"",
            $"set \"TRACE=%HERE%{safe}\"",
            "set \"PW=%HERE%..\\playwright.ps1\"",
            "if not exist \"%PW%\" ( echo Run the E2E tests first. & pause & exit /b 1 )",
            "powershell -NoProfile -ExecutionPolicy Bypass -File \"%PW%\" show-trace \"%TRACE%\"", "");
        var path = Path.Combine(ArtifactsDir, $"{name}.trace.cmd");
        File.WriteAllText(path, cmd);
        return path;
    }

    /// <summary>
    /// Signs in as a persona (<c>P-01</c>..<c>P-07</c>) using its seeded credentials. Pass
    /// <paramref name="forceLogin"/> to bypass the cached-token fast path and drive the real
    /// login+MFA UI (used by tests that verify the login flow itself).
    /// </summary>
    protected Task SignInAsync(string persona, bool forceLogin = false)
    {
        var creds = Settings.ForPersona(persona);
        return SignInAsync(creds.Email, creds.Password, creds.TotpSecret, forceLogin);
    }

    /// <summary>
    /// Convenience overload signing in as the active, MFA-enrolled seeded user (== P-01). Pass
    /// <paramref name="forceLogin"/> to force the real login+MFA UI instead of reusing a cached token.
    /// </summary>
    protected Task SignInAsync(bool forceLogin = false) =>
        SignInAsync(Settings.Email, Settings.Password, Settings.TotpSecret, forceLogin);

    // Session tokens cached per user (by email) for this serial ([DoNotParallelize]) run, so only
    // the FIRST sign-in per user runs the real login+MFA flow; every later sign-in boots the app
    // already-authenticated by re-seeding the token — no login form, no TOTP, no 30s anti-replay
    // wait. A plain static dictionary is safe because sign-ins never run concurrently.
    private static readonly Dictionary<string, string> CachedTokens = new();

    /// <summary>
    /// Signs in as the given user. By default reuses a cached <c>sessionStorage.session_token</c>
    /// when one is available and still valid (skipping the login form + TOTP entirely), falling back
    /// to the real login+MFA UI on a cache miss or a rejected (stale) token. Pass
    /// <paramref name="forceLogin"/> to <b>always</b> drive the real login UI — tests that verify the
    /// login/MFA flow itself MUST set it, or they would short-circuit through the cache and prove
    /// nothing. Either path caches the resulting token for the next reuse.
    /// </summary>
    protected async Task SignInAsync(string email, string password, string totpSecret, bool forceLogin = false)
    {
        if (!forceLogin && CachedTokens.TryGetValue(email, out var token) && await TryResumeSessionAsync(token))
        {
            return;
        }

        CachedTokens.Remove(email); // forced, missing, or stale → real sign-in
        await LoginThroughUiAsync(email, password, totpSecret);
        CachedTokens[email] = await Page.EvaluateAsync<string>(
            "() => window.sessionStorage.getItem('session_token')");
    }

    /// <summary>
    /// Drives the real auth flow for an MFA-enrolled user: login form → MFA challenge (TOTP) →
    /// authenticated session, landing the opaque token in <c>sessionStorage.session_token</c>.
    /// Always goes through the UI (no token reuse); reached via <see cref="SignInAsync(string,string,string,bool)"/>.
    /// </summary>
    private async Task LoginThroughUiAsync(string email, string password, string totpSecret)
    {
        await SubmitCredentialsAsync(email, password);
        await CompleteMfaWithRetryAsync(totpSecret);
        await WaitForSessionTokenAsync();
    }

    /// <summary>
    /// Seeds a cached token into <c>sessionStorage</c> and boots the app; returns <c>true</c> if it
    /// lands authenticated, <c>false</c> if the app rejects it (the AuthGuard bounces to <c>/login</c>).
    /// Uses an Evaluate-then-renavigate (NOT a Playwright init script) so a rejected token leaves no
    /// lingering injection that would re-overwrite a fresh token on the real-sign-in fallback path.
    /// </summary>
    private async Task<bool> TryResumeSessionAsync(string token)
    {
        // sessionStorage is origin-scoped — load the origin once before writing the key.
        await Page.GotoAsync($"{BaseUrl}/login");
        await Page.EvaluateAsync("t => window.sessionStorage.setItem('session_token', t)", token);
        await Page.GotoAsync(BaseUrl);

        // A rejected token makes the AuthGuard redirect to /login; a valid one never does. Wait for
        // the bounce rather than reading Page.Url immediately (the redirect is client-side and races
        // GotoAsync's completion) — no bounce within the window means the session resumed.
        try
        {
            await Page.WaitForURLAsync(new Regex(@"/login"), new PageWaitForURLOptions { Timeout = 2500 });
            return false; // bounced → stale
        }
        catch (Exception)
        {
            return true; // still authenticated
        }
    }

    // Last 30s TOTP step actually SUBMITTED per secret this run; sign-ins are serial
    // ([DoNotParallelize]). Recorded at submit time (not credential-submit time) so a boundary
    // crossing can't make us replay a code — a replayed code is rejected AND increments the
    // account's failed-attempt count, which would lock the shared fixture user for the rest of the run.
    private static readonly Dictionary<string, long> LastSubmittedTotpStep = new();

    private static long CurrentTotpStep() => DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;

    /// <summary>
    /// Step 1 of login: navigate to <c>/login</c>, fill the email + password fields, and submit.
    /// Stops before the MFA step so callers can branch on the resulting route (challenge vs.
    /// enrollment) themselves.
    /// </summary>
    protected async Task SubmitCredentialsAsync(string email, string password)
    {
        await Page.GotoAsync($"{BaseUrl}/login");
        await Page.Locator("#email").FillAsync(email);
        await Page.Locator("#password").FillAsync(password);
        await Page.Locator("button[type=submit]").First.ClickAsync();
    }

    private static readonly Regex AuthenticatedUrl = new(@"^(?!.*/(login|auth|mfa)).*$");

    /// <summary>
    /// Enters the 6-digit TOTP, retrying on the next step boundary if the code is refused
    /// (backend anti-replay rejects a code reused within its 30s window — relevant when shared
    /// fixture accounts sign in back-to-back). Clears the OTP entry between attempts so the
    /// page's onComplete re-fires.
    /// </summary>
    private async Task CompleteMfaWithRetryAsync(string totpSecret)
    {
        const int maxAttempts = 4;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            await SubmitFreshTotpAsync(totpSecret);
            try
            {
                await Page.WaitForURLAsync(AuthenticatedUrl, new PageWaitForURLOptions
                {
                    Timeout = attempt == maxAttempts ? 15000 : 8000,
                });
                return;
            }
            catch (Exception) when (attempt < maxAttempts)
            {
                // Code refused (anti-replay) or transient miss — clear and re-enter a fresh code.
                await ClearOtpAsync();
            }
        }
    }

    /// <summary>
    /// Submits a TOTP code guaranteed fresh for this secret: if the current 30s step was already
    /// submitted this run, waits for the next step first, then records the step at submission. The
    /// code is entered with <c>FillAsync</c> — input-otp's controlled input fires onComplete (which
    /// auto-submits the MFA challenge).
    /// </summary>
    private async Task SubmitFreshTotpAsync(string secret)
    {
        if (LastSubmittedTotpStep.TryGetValue(secret, out var used) && used >= CurrentTotpStep())
        {
            await WaitForNextTotpStepAsync();
        }

        LastSubmittedTotpStep[secret] = CurrentTotpStep();
        await FillOtpAsync(ComputeTotp(secret));
    }

    /// <summary>Polls <c>sessionStorage.session_token</c> until the SPA has stored the bearer token.</summary>
    private async Task WaitForSessionTokenAsync()
    {
        for (var i = 0; i < 30; i++)
        {
            var token = await Page.EvaluateAsync<string?>("() => window.sessionStorage.getItem('session_token')");
            if (!string.IsNullOrEmpty(token))
            {
                return;
            }

            await Page.WaitForTimeoutAsync(200);
        }

        Assert.Fail("Sign-in did not store a session_token in sessionStorage within the timeout.");
    }

    private static Task WaitForNextTotpStepAsync()
    {
        var secondsIntoStep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 30;
        return Task.Delay(TimeSpan.FromSeconds(30 - secondsIntoStep + 1));
    }

    /// <summary>
    /// Fills the 6-digit TOTP entry (shadcn <c>input-otp</c> renders a single hidden input carrying
    /// the whole value). Typing the full value fires the page's <c>onComplete</c> handler.
    /// </summary>
    protected Task FillOtpAsync(string code) => OtpInput.FillAsync(code);

    /// <summary>Clears the OTP entry so a subsequent fill re-fires the page's onComplete.</summary>
    protected Task ClearOtpAsync() => OtpInput.FillAsync(string.Empty);

    private ILocator OtpInput =>
        Page.Locator("input[autocomplete='one-time-code'], input[name='otp'], input[inputmode='numeric']").First;

    /// <summary>Computes the current TOTP code for a Base32 shared secret.</summary>
    protected static string ComputeTotp(string base32Secret) =>
        new Totp(Base32Encoding.ToBytes(base32Secret)).ComputeTotp();

    /// <summary>
    /// Re-seeds the MFA-enrollment fixture account back to its pending-enrollment state via the
    /// Development-only fixture endpoint, so the MFA-enrollment flow can be exercised on every run.
    /// Targets the single seeded enrollment account, so it's safe under parallel execution.
    /// </summary>
    protected async Task ReseedEnrollUserAsync()
    {
        var response = await Context.APIRequest.PostAsync($"{BaseUrl}/api/v1/dev/fixtures/reseed-enroll");
        Assert.IsTrue(response.Ok, $"reseed-enroll fixture endpoint failed ({response.Status}).");
    }

    /// <summary>
    /// Mints a fresh single-use password-reset token for a user via the Development-only fixture
    /// endpoint (clearing prior tokens + rate-limit state), and returns the raw token to redeem.
    /// </summary>
    protected async Task<string> IssueResetTokenAsync(string email)
    {
        var response = await Context.APIRequest.PostAsync(
            $"{BaseUrl}/api/v1/dev/fixtures/issue-reset-token",
            new() { DataObject = new { email } });
        Assert.IsTrue(response.Ok, $"issue-reset-token fixture endpoint failed ({response.Status}) for {email}.");
        var json = await response.JsonAsync();
        return json!.Value.GetProperty("token").GetString()!;
    }

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return name;
    }
}
