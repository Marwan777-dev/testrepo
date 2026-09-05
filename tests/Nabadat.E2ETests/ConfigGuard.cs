using Nabadat.E2ETests.Infrastructure;

namespace Nabadat.E2ETests;

/// <summary>
/// Fails the whole E2E run once, up front, if any required fixture is missing —
/// instead of letting individual tests fail deep in a browser flow with an opaque
/// selector timeout. A missing fixture is a setup gap to fill in
/// <c>appsettings.local.json</c> (gitignored) or to ask the user for; the harness
/// will NOT seed or guess accounts/secrets/tokens.
///
/// Keep <see cref="Required"/> in sync with the keys the tests actually read
/// (see <see cref="E2ESettings"/>). A key the user is explicitly asked about and
/// chooses to leave out should be dropped from this list AND its test marked
/// <c>Assert.Inconclusive</c> + a <c>not covered — fixture declined</c> row in
/// COVERAGE.md.
/// </summary>
[TestClass]
public static class ConfigGuard
{
    [AssemblyInitialize]
    public static void Validate(TestContext _)
    {
        var s = E2ESettings.Load();

        // All US1 fixtures are now seeded by the host's DevDataSeeder (Development),
        // so every auth test has the data it needs and we require the full set up
        // front. (The Assert.Inconclusive guards in AuthTests remain as a fallback
        // for environments that deliberately omit a fixture and drop it from here.)
        var required = new (string Key, string Value, string Purpose)[]
        {
            ("E2E_BASE_URL", s.BaseUrl, "running SPA dev-server URL (e.g. http://localhost:5173)"),
            ("E2E_EMAIL", s.Email, "active, MFA-enrolled user — T064/T066"),
            ("E2E_PASSWORD", s.Password, "password for the active user — T064/T066"),
            ("E2E_TOTP_SECRET", s.TotpSecret, "Base32 TOTP secret for the active user — T064"),
            ("E2E_ENROL_EMAIL", s.EnrolEmail, "not-yet-enrolled user — T065"),
            ("E2E_ENROL_PASSWORD", s.EnrolPassword, "password for the not-yet-enrolled user — T065"),
            ("E2E_RESET_EMAIL", s.ResetEmail, "user a fresh reset token is minted for — T067/T068"),
            ("E2E_P07_EMAIL", s.P07Email, "active, MFA-enrolled P-07 (Tenant Administrator) — T095"),
            ("E2E_P07_PASSWORD", s.P07Password, "password for the P-07 user — T095"),
            ("E2E_P07_TOTP_SECRET", s.P07TotpSecret, "Base32 TOTP secret for the P-07 user — T095"),
            ("E2E_P03_EMAIL", s.P03Email, "active, MFA-enrolled P-03 with no module grants — T096"),
            ("E2E_P03_PASSWORD", s.P03Password, "password for the P-03 user — T096"),
            ("E2E_P03_TOTP_SECRET", s.P03TotpSecret, "Base32 TOTP secret for the P-03 user — T096"),
            ("E2E_P02_EMAIL", s.P02Email, "active, MFA-enrolled P-02 (CX Analyst, full CX modules) — M-16 T080"),
            ("E2E_P02_PASSWORD", s.P02Password, "password for the P-02 user — M-16 T080"),
            ("E2E_P02_TOTP_SECRET", s.P02TotpSecret, "Base32 TOTP secret for the P-02 user — M-16 T080"),
        };

        var missing = required.Where(r => string.IsNullOrWhiteSpace(r.Value)).ToArray();
        if (missing.Length == 0)
        {
            return;
        }

        var lines = missing.Select(m => $"  - {m.Key}: {m.Purpose}");
        throw new InvalidOperationException(
            "E2E config incomplete. Fill these in tests/Nabadat.E2ETests/appsettings.local.json " +
            "(gitignored; copy from appsettings.local.json.example) or set the matching environment variables. " +
            "The harness will NOT seed or guess them — ask the user for the values:" +
            Environment.NewLine + string.Join(Environment.NewLine, lines));
    }
}
