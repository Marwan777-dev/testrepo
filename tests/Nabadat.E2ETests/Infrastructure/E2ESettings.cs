using System.Text.Json;

namespace Nabadat.E2ETests.Infrastructure;

/// <summary>
/// E2E run configuration for the single `frontend/` SPA: the base URL of the running dev server,
/// the seeded auth fixtures the M-10 auth tests drive, and the seeded persona fixtures the KPI /
/// journey tests drive. Read from the gitignored <c>appsettings.local.json</c> beside the test
/// assembly (template: <c>appsettings.local.json.example</c>), with <c>E2E_*</c> environment
/// overrides for CI.
///
/// <para>Personas: P-01 (CX Program Manager, authoring), P-02 (CX Analyst), P-03 (read-only, no
/// module grants), P-06 (Executive Sponsor), P-07 (Tenant IT Administrator). The flat
/// <see cref="Email"/>/<see cref="Password"/>/<see cref="TotpSecret"/> map to the active,
/// MFA-enrolled user (the same account as P-01) used by <see cref="E2ETestBase.SignInAsync()"/>.</para>
/// </summary>
public sealed class E2ESettings
{
    // Multi-tenant dev resolves the tenant from the subdomain, so E2E targets a tenant subdomain
    // (the dedicated 'e2e' tenant). Override via the e2e:baseUrl config key or E2E_BASE_URL.
    public string BaseUrl { get; init; } = "http://e2e.localhost:5173";

    /// <summary>
    /// Tenant-database connection string used ONLY by the KPI deactivation-confirmation test
    /// (<see cref="E2ETenantDb"/>) to seed the one M-16 <c>kpi_bindings</c> row no UI can create.
    /// Empty when not configured (that one test then skips with a clear reason).
    /// </summary>
    public string TenantDb { get; init; } = string.Empty;

    /// <summary>The tenant schema the bound-KPI rows live in (multi-tenant: <c>tenant_{slug}</c>, dev slug <c>e2e</c>).</summary>
    public string TenantSchema { get; init; } = "tenant_e2e";

    // Active, MFA-enrolled user (== P-01) — SignInAsync() convenience + AUTH-1/AUTH-3.
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string TotpSecret { get; init; } = string.Empty;

    // A user that has NOT yet enrolled an MFA factor (AUTH-2 enrollment flow).
    public string EnrolEmail { get; init; } = string.Empty;
    public string EnrolPassword { get; init; } = string.Empty;

    // The user a fresh single-use reset token is minted for (AUTH-4/AUTH-5).
    public string ResetEmail { get; init; } = string.Empty;

    public PersonaCredentials P01 { get; init; } = new();
    public PersonaCredentials P02 { get; init; } = new();
    public PersonaCredentials P03 { get; init; } = new();
    public PersonaCredentials P06 { get; init; } = new();
    public PersonaCredentials P07 { get; init; } = new();

    // Flat accessors kept for tests that read persona creds directly (e.g. SignInAsync(P07Email, …)).
    public string P02Email => P02.Email;
    public string P02Password => P02.Password;
    public string P02TotpSecret => P02.TotpSecret;
    public string P03Email => P03.Email;
    public string P03Password => P03.Password;
    public string P03TotpSecret => P03.TotpSecret;
    public string P07Email => P07.Email;
    public string P07Password => P07.Password;
    public string P07TotpSecret => P07.TotpSecret;

    /// <summary>Resolves the credentials for a persona code (<c>P-01</c>..<c>P-07</c>).</summary>
    public PersonaCredentials ForPersona(string persona) => persona switch
    {
        "P-01" => P01,
        "P-02" => P02,
        "P-03" => P03,
        "P-06" => P06,
        "P-07" => P07,
        _ => throw new ArgumentOutOfRangeException(nameof(persona), persona, "No seeded credentials for this persona."),
    };

    public static E2ESettings Load()
    {
        var f = LoadFromFile();
        string Env(string key, string fallback) => Environment.GetEnvironmentVariable(key) ?? fallback;

        return new E2ESettings
        {
            BaseUrl = Env("E2E_BASE_URL", f.BaseUrl),
            TenantDb = Env("E2E_TENANT_DB", f.TenantDb),
            TenantSchema = Env("E2E_TENANT_SCHEMA", f.TenantSchema),
            Email = Env("E2E_EMAIL", f.Email),
            Password = Env("E2E_PASSWORD", f.Password),
            TotpSecret = Env("E2E_TOTP_SECRET", f.TotpSecret),
            EnrolEmail = Env("E2E_ENROL_EMAIL", f.EnrolEmail),
            EnrolPassword = Env("E2E_ENROL_PASSWORD", f.EnrolPassword),
            ResetEmail = Env("E2E_RESET_EMAIL", f.ResetEmail),
            P01 = f.P01.WithEnv("E2E_P01"),
            P02 = f.P02.WithEnv("E2E_P02"),
            P03 = f.P03.WithEnv("E2E_P03"),
            P06 = f.P06.WithEnv("E2E_P06"),
            P07 = f.P07.WithEnv("E2E_P07"),
        };
    }

    private static E2ESettings LoadFromFile()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.local.json");
        if (!File.Exists(path))
        {
            return new E2ESettings();
        }

        using var stream = File.OpenRead(path);
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;
        var e2e = root.TryGetProperty("e2e", out var section) ? section : root;

        return new E2ESettings
        {
            BaseUrl = GetString(e2e, "baseUrl") ?? new E2ESettings().BaseUrl,
            TenantDb = GetString(e2e, "tenantDb") ?? string.Empty,
            TenantSchema = GetString(e2e, "tenantSchema") ?? new E2ESettings().TenantSchema,
            Email = GetString(e2e, "email") ?? string.Empty,
            Password = GetString(e2e, "password") ?? string.Empty,
            TotpSecret = GetString(e2e, "totpSecret") ?? string.Empty,
            EnrolEmail = GetString(e2e, "enrolEmail") ?? string.Empty,
            EnrolPassword = GetString(e2e, "enrolPassword") ?? string.Empty,
            ResetEmail = GetString(e2e, "resetEmail") ?? string.Empty,
            P01 = PersonaCredentials.Read(e2e, "p01"),
            P02 = PersonaCredentials.Read(e2e, "p02"),
            P03 = PersonaCredentials.Read(e2e, "p03"),
            P06 = PersonaCredentials.Read(e2e, "p06"),
            P07 = PersonaCredentials.Read(e2e, "p07"),
        };
    }

    internal static string? GetString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value)
            ? value.GetString()
            : null;
}

/// <summary>Seeded credentials for one persona: email + password + MFA TOTP secret.</summary>
public sealed class PersonaCredentials
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string TotpSecret { get; init; } = string.Empty;

    public static PersonaCredentials Read(JsonElement e2e, string prefix) => new()
    {
        Email = E2ESettings.GetString(e2e, $"{prefix}Email") ?? string.Empty,
        Password = E2ESettings.GetString(e2e, $"{prefix}Password") ?? string.Empty,
        TotpSecret = E2ESettings.GetString(e2e, $"{prefix}TotpSecret") ?? string.Empty,
    };

    /// <summary>Returns a copy with any <c>{envPrefix}_EMAIL/_PASSWORD/_TOTP_SECRET</c> env vars applied over the file values.</summary>
    public PersonaCredentials WithEnv(string envPrefix) => new()
    {
        Email = Environment.GetEnvironmentVariable($"{envPrefix}_EMAIL") ?? Email,
        Password = Environment.GetEnvironmentVariable($"{envPrefix}_PASSWORD") ?? Password,
        TotpSecret = Environment.GetEnvironmentVariable($"{envPrefix}_TOTP_SECRET") ?? TotpSecret,
    };
}
