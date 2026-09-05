---
name: e2e-testing
description: >-
  Generate permanent browser end-to-end (E2E) tests for any Nabadat frontend SPA workspace
  (frontend/portal/, frontend/tenant-app/, …; each a Vite + React 19 app) from its specs or
  code, using Playwright driven from an MSTest project (Microsoft.Playwright.MSTest). Use
  when asked to build/extend browser/UI regression coverage, derive an E2E coverage matrix
  from spec.md user stories or the React pages/routes, or turn a described user flow into
  running browser tests. Reads the specs/code, writes a COVERAGE.md matrix, then generates
  [TestMethod] tests into <Feature>Tests.cs and runs them against a RUNNING instance of the
  app (set via E2E_BASE_URL), capturing a screenshot + trace per test and attaching them to
  the test result (visible in the VS Test Explorer Attachments section). One E2E project
  (tests/Nabadat.E2ETests/) covers the single frontend app, with tests grouped into
  module-named folders sharing one harness (Infrastructure/E2ETestBase.cs). This is the browser/UI lane
  — it sits ON TOP OF, and does not replace, the backend unit / integration / scenario /
  contract lanes. For those non-browser .NET tests, use the backend xUnit projects per the
  project-root CLAUDE.md Unit Test Policy.
---

# Nabadat Frontend — E2E (Browser) Testing

Turn **specifications** (`specs/<feature>/spec.md`, plus any `.md` / `.pdf` in the repo)
or the **React code itself** into a **test-coverage matrix**, then generate **permanent
browser E2E tests** from that matrix — written straight into a `<Feature>Tests.cs` class,
run against the **running** SPA, and captured with screenshots + traces. There is **no
scratch file**: every test you write is a keeper that lands in a named feature class.

## Project layout

The lane lives in **one project, `tests/Nabadat.E2ETests/`**, covering the repo's single `frontend/`
SPA (there is **no** `frontend/portal/` or `frontend/tenant-app/` subfolder today). Tests are grouped
into **module-named folders** (`KpiManagement/`, `CustomerJourneyManagement/`, `UserManagement/`, …)
mirroring the `Nabadat.<Module>` unit/integration taxonomy. All tests share **one harness**:

| Harness (in `tests/Nabadat.E2ETests/`) | Default `E2E_BASE_URL` | Backend host (proxied `/api`) | Login flow / session store |
|----------------------------------------|------------------------|-------------------------------|----------------------------|
| `Infrastructure/E2ETestBase.cs` | `http://e2e.localhost:5173` | `https://localhost:7286` | `/login` → MFA challenge (input-otp TOTP) → `sessionStorage.session_token` |

Place your `<Feature>Tests.cs` in the module folder it belongs to (namespace
`Nabadat.E2ETests.<Module>`) and have it extend `E2ETestBase`. Set `E2E_BASE_URL` to the running dev
server and `--filter` to your module/feature tests.

> **If a second, separately authenticated SPA is ever added** (e.g. a future tenant-app on its own
> host with a different login flow), give it its own `Infrastructure/<App>/E2ETestBase.cs` in this
> same project — do **not** create a second project, and do **not** branch the existing harness.

> **⚠️ `E2E_BASE_URL` scheme must match what the Vite dev server actually serves — DERIVE it,
> don't copy a table.** The scheme differs per workspace: `frontend/portal/` serves **HTTP**
> (`http://localhost:5173`), `frontend/tenant-app/` serves **HTTPS**
> (`https://localhost:5173`, self-signed cert via `@vitejs/plugin-basic-ssl`). Pointing
> `E2E_BASE_URL` at the wrong scheme breaks the very first `GotoAsync`: `https://` against an
> HTTP server (or `http://` against an HTTPS server) yields `net::ERR_SSL_PROTOCOL_ERROR` /
> `ERR_EMPTY_RESPONSE`, and `IgnoreHTTPSErrors` does **not** help (it covers cert validation,
> not a missing/extra TLS layer). So **read the workspace's `vite.config.ts`** rather than
> trusting any table: `server.https` unset ⇒ HTTP; `server.https` set (`{}`, `true`, or a cert
> object, often via a `basicSsl()` plugin) ⇒ HTTPS. Set `E2E_BASE_URL` (and
> `appsettings.local.json`) to match the *running* server. If the first `GotoAsync` throws a
> scheme error, the scheme is inverted — flip it. For HTTPS dev servers the `E2ETestBase`
> already sets `IgnoreHTTPSErrors = true` so the self-signed cert is accepted.
>
> **⚠️ The `/api` proxy must reach the backend WITHOUT an HTTP→HTTPS redirect.** If
> authenticated calls return 401 immediately after a *successful* login (you land on a page,
> then bounce back to `/login`), suspect a **307**: the backend's `UseHttpsRedirection`
> 307-redirects the proxied HTTP request to its HTTPS origin, the browser follows it
> **cross-origin** and per the fetch spec **strips the `Authorization` header** → 401 → session
> cleared → redirect to `/login`. Anonymous calls (login/TOTP) survive because they carry no
> header, so it masquerades as an MFA/login bug. Fix the proxy, not the test: run the backend on
> its **http** launch profile (no HTTPS endpoint ⇒ `UseHttpsRedirection` is a no-op), or point
> the proxy at the backend's **https** port directly. Confirm via the trace's network tab (look
> for `307` on `/api/...`).
>
> Both workspaces dodge the 307 the same way: the Vite proxy targets the backend's **HTTPS**
> port directly (portal → `https://localhost:7286`, tenant-app → `https://localhost:7003`)
> with `secure: false`, so the proxied request is already HTTPS and `UseHttpsRedirection` has
> nothing to redirect. For the tenant-app, that means running the host on its **`https`**
> launch profile (`dotnet run --project src/Nabadat.TenantApp --launch-profile https`) so
> `:7003` is bound. (One-time: `dotnet dev-certs https --trust` so the backend has a dev cert.)

> **This repo (Nabadat).** Each app under test is a **Vite + React 19 SPA** (Tailwind 4 +
> shadcn/ui + React Router + i18next). Routes are declared in the workspace's
> `src/App.tsx`; pages live in `src/pages/**.tsx` or `src/routes/**.tsx` and feature
> components in `src/features/<feature>/` and `src/components/`. Each SPA talks to its .NET
> backend through the Vite dev-server `/api` proxy. The worked example further down (a
> generic sign-in → list → assert flow) shows the *technique*; rediscover the real routes,
> fields, button text, and accounts for your workspace from its `.tsx` pages and the seed
> data — do not trust any hardcoded table.

> **Relationship to the backend test lanes.** The backend lanes (unit / integration /
> scenario / contract, xUnit v3 under `tests/Nabadat.*`) cover pure logic, real-DB
> Testcontainers flows, and HTTP endpoints. Per project-root `CLAUDE.md`, **frontend unit
> testing (Vitest) is NOT enforced** by the spec-kit flow. This skill is the **frontend's
> enforced lane** — the browser layer that proves the React pages actually render and the
> user journeys actually work end-to-end. It complements the backend lanes; it does not
> replace them.
>
> **Why MSTest here (not xUnit) — deliberate exception.** The backend lanes use xUnit v3,
> but this browser lane uses **MSTest with `Microsoft.Playwright.MSTest`**. Two reasons:
> (1) Playwright ships an official `PageTest` base class for MSTest/NUnit but **not**
> xUnit; (2) test-result **attachments** added via MSTest's `TestContext.AddResultFile`
> render reliably in the VS Test Explorer *Attachments* section, whereas xUnit v3's
> `AddAttachment` is ignored by Test Explorer under Microsoft Testing Platform mode. The
> screenshot + trace attachments are a core feature of this lane, so MSTest is the right
> tool for it.
>
> **Playwright is language-agnostic about the app.** Playwright drives a real browser over
> a WebSocket protocol; from the browser's point of view the React SPA is just HTML + JS +
> CSS served over HTTP. The .NET test code never touches React or TypeScript — it only
> sees the *rendered DOM*. That is why a .NET MSTest project drives a React app with no
> friction (the same way the upstream gac harness drives an ASP.NET MVC 5 app it doesn't
> even reference): Playwright only needs a URL.

## Why the harness targets an already-running instance

The portal is a Vite SPA, not a Kestrel app — there is no in-process
`WebApplicationFactory` to boot. E2E tests therefore do **not** auto-start anything. They
target an **already-running** portal via the `E2E_BASE_URL` environment variable: you
start the stack yourself, then run the tests. Because Playwright only needs an HTTP URL,
the E2E project is a **standalone modern SDK project that references no other project** —
it neither builds nor imports the React app.

The two things the harness owns (reuse, don't rebuild):
- **`E2ETestBase.cs`** — a `PageTest`-derived base that, per test, starts a trace and on
  cleanup writes a **screenshot and a full trace for every test** (pass *or* fail) named
  after the test (`<TestName>.png` / `<TestName>.zip`), **attaches both — plus a
  `<TestName>.trace.cmd` launcher — to the test result** via `TestContext.AddResultFile`
  (so they show in the VS Test Explorer *Attachments* section + the TRX), plus a
  `SignInAsync` + `BaseUrl` helper.
- **`COVERAGE.md`** — the traceable matrix; one row per `[TestMethod]`.

## Credentials (ask the user — NEVER seed or create accounts)

The browser lane signs in as a **real, already-existing** account. This skill must **never
create, seed, or migrate a user** to make tests pass — provisioning a login is the app's
job, not the test harness's. Treat sign-in credentials as **inputs you obtain**, not
fixtures you manufacture.

**Do this FIRST, before authoring or running anything.** The harness `Config` reads these
keys — `E2E_USER`, `E2E_PASSWORD`, `E2E_TOTP_SECRET` (the Base32 secret of the account's
enrolled factor, for MFA-gated workspaces like the tenant-app), and `E2E_BASE_URL` — with
this precedence (highest first):

1. **`appsettings.local.json`** beside the test project (gitignored; copy from
   `appsettings.local.example.json`) — the authoritative source for **local dev**. It wins
   on purpose so stray machine/CI `E2E_*` environment variables (e.g. left over from another
   project) can't silently hijack the run.
2. **Environment variables** of the same names — apply in **CI**, where the local file is
   absent.

So for local runs, put the values in `appsettings.local.json`; for CI, set the env vars.

**If `E2E_USER` or `E2E_PASSWORD` (or the TOTP secret, where the login is MFA-gated) is
missing, STOP and ASK THE USER for them** (use AskUserQuestion, or ask in plain text):

> "The E2E browser lane needs an existing test login. Please provide `E2E_USER` and
> `E2E_PASSWORD` (and the Base32 `E2E_TOTP_SECRET` for the account's MFA factor, since this
> workspace's login is MFA-gated). I will NOT create or seed a new account."

- When the user provides them, write them into `appsettings.local.json` (copy from
  `appsettings.local.example.json`) and proceed. (Env vars also work, but for local runs the
  local file is authoritative — see precedence above.)
- When the user declines or doesn't have them, do **not** invent or seed an account.
  Proceed only with the tests that need no login (e.g. the session-expiry / unauthenticated
  redirect tests). For the scenarios you're dropping, **omit their tests and remove their
  keys from the run** (see the Config auto-detection cycle below) and mark the COVERAGE.md
  rows `not covered — fixture declined` — a recorded scope decision, not a silent yellow skip
  and not a blank-but-Required key.

### Config auto-detection cycle — detect every key the tests read, fill or ASK (never silently undefined)

`E2E_USER` / `E2E_PASSWORD` / `E2E_TOTP_SECRET` are only the *baseline* login keys. Real
suites routinely read **more** keys — a **deactivated** account (`E2E_DEACTIVATED_USER` /
`E2E_DEACTIVATED_PASSWORD`, AUTH-4), a **not-yet-enrolled** account (`E2E_ENROL_USER` /
`E2E_ENROL_PASSWORD`, AUTH-3), a **non-admin** account, a disposable target id, a tenant
slug, and so on.

**This is a GENERAL, ENFORCED mechanism — not a fixed list and not a one-off for today's
keys.** It applies to every key any test reads (current or added later) and to **any
parameter you cannot seed or guess**. Do not hard-code "the three baseline keys"; derive the
required set from the code every time, and for anything un-seedable, **ASK THE USER**.

**The cycle — run it before every run, and AGAIN after authoring/adding any test:**

1. **Auto-detect the required keys (derive, don't hand-list).** The typed accessors
   (`SeededUser()`, `DeactivatedUser()`, `EnrolmentUser()`, …) all bottom out in
   `Config["…"]`, so one scan over the test project yields every key:
   ```powershell
   (Select-String -Path tests/Nabadat.E2ETests/*.cs,tests/Nabadat.E2ETests/**/*.cs `
       -Pattern 'Config\["([^"]+)"\]' -AllMatches).Matches |
     ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
   ```
   (or `rg -o 'Config\["([^"]+)"\]' -r '$1' <project> --no-filename | sort -u`).
2. **Diff against what's filled.** Read `appsettings.local.json`; a key is *satisfied* only
   if present AND non-empty. `required − satisfied = missing`.
3. **Resolve the missing set.** Fill what you can legitimately derive (e.g. `E2E_BASE_URL`
   from `vite.config.ts`). For anything you **cannot seed or guess** — credentials, secrets,
   slugs, ids — **STOP and ASK THE USER** in one prompt that lists *every* missing key and
   what each needs (active+enrolled / deactivated / not-yet-enrolled / non-admin / …). Never
   invent, seed, or mutate to manufacture a value. Write the answers into
   `appsettings.local.json` (and keep `appsettings.local.example.json` carrying a placeholder
   row for every key, so nothing is an invisible dependency).
4. **Re-detect, don't assume.** After writing answers, re-run steps 1–2 until `missing` is
   empty. Authoring a new test that reads a new key re-opens the cycle — run it again at the
   end of authoring, before the final run.

**Enforce it in the harness so a gap can't pass silently.** Do NOT default to per-test
`Assert.Inconclusive` on a blank value — that hides the gap as a yellow "covered" result.
Instead, an assembly-level guard fails the whole run once, enumerating ALL missing keys:

```csharp
[TestClass]
public static class ConfigGuard
{
    // Runtime contract — must match the auto-detect scan (step 1), which is the source of
    // truth. When a new test introduces a key, the cycle adds it here too.
    private static readonly string[] Required =
        { "E2E_BASE_URL", "E2E_USER", "E2E_PASSWORD", "E2E_TOTP_SECRET",
          "E2E_ENROL_USER", "E2E_ENROL_PASSWORD",
          "E2E_DEACTIVATED_USER", "E2E_DEACTIVATED_PASSWORD" };

    [AssemblyInitialize]
    public static void Validate(TestContext _)
    {
        var missing = Required.Where(k => string.IsNullOrWhiteSpace(E2ETestBase.Config[k])).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException(
                "E2E config incomplete — fill these in appsettings.local.json (gitignored): " +
                string.Join(", ", missing) +
                ". The harness will NOT seed or guess them — ask the user for the values.");
    }
}
```

(Expose `Config` as `internal static` on `E2ETestBase` so the guard can read it.) A missing
fixture is now a single, loud failure naming every gap — not N silent yellow skips.
`Assert.Inconclusive` is legitimate ONLY for a key the user was explicitly asked about and
**chose** to leave out; in that case also drop the key from `Required` and mark the
COVERAGE.md row `not covered — fixture declined`, so a declined scope is a recorded decision,
never a blank-but-Required key (which the guard would fail).

> **Why no seeding.** Manufacturing a user couples the test lane to the app's auth internals
> (password hashing, TOTP encryption keys, role wiring) and risks planting a known-credential
> account where it shouldn't exist. If no suitable test account exists, that's a gap for the
> team to fill in their seed/provisioning process — surface it; don't paper over it here.

## Starting the stack for E2E (do this before running tests)

A workspace's sign-in and data calls hit its backend through the Vite dev-server `/api`
proxy (each workspace's `vite.config.ts` declares the proxy target). So the **dev server is
the right target** (not `vite preview`, which has no proxy). Bring up the full stack — the
example below is for `frontend/portal/` (dev server `http://localhost:5173`); for
`frontend/tenant-app/` the dev server is **HTTPS** (`https://localhost:5173`) and the
TenantApp host runs on its **https** profile (`--launch-profile https`, binds `:7003`) — see
the workspace table and the scheme/proxy cautions above:

```powershell
# 1. Postgres (local dev: localhost:5433, user=postgres, password=admin — see project memory).
#    Ensure it is running and the test tenant schema is migrated/seeded.

# 2. Backend host:
#    portal     → dotnet run --project src/Nabadat.TenantApp                     (https://localhost:7286)
#    tenant-app → dotnet run --project src/Nabadat.TenantApp --launch-profile https  (https://localhost:7003)

# 3. The workspace's dev server, which proxies /api to the backend's HTTPS port:
#    (run from the workspace; portal → http://localhost:5173, tenant-app → https://localhost:5173)
npm run dev

# 4. In the shell you run the tests from, point E2E_BASE_URL at that dev server — MATCH THE SCHEME:
$env:E2E_BASE_URL = "http://localhost:5173"    # portal (HTTP)
# $env:E2E_BASE_URL = "https://localhost:5173"  # tenant-app (HTTPS) — see the scheme caution above
```

If `E2E_BASE_URL` is unset, the tests fail fast with a message telling you to start the
app — they never silently pass against nothing.

## Paths & commands

Stated once; referenced throughout. `<Feature>` = the feature class you're filling
(e.g. `Sessions`, `Invoices`); `<TestName>` = a test method name inside it.

`<Module>` = the module folder your feature belongs to (`KpiManagement`, `UserManagement`, …).

| | |
|---|---|
| Coverage matrix | `tests/Nabadat.E2ETests/COVERAGE.md` (one matrix for the whole project) |
| Feature test file | `tests/Nabadat.E2ETests/<Module>/<Feature>Tests.cs` (namespace `Nabadat.E2ETests.<Module>`) |
| Harness base class | `tests/Nabadat.E2ETests/Infrastructure/E2ETestBase.cs` (the one shared harness) |
| Local creds/config | `tests/Nabadat.E2ETests/appsettings.local.json` (gitignored; copy from `appsettings.local.json.example`) |
| Artifacts dir | `tests/Nabadat.E2ETests/bin/Debug/net10.0/playwright-artifacts/` |
| Report | `tests/Nabadat.E2ETests/TestResults/<Feature>.trx` (written by the Run command) |
| **Run** | `dotnet test tests/Nabadat.E2ETests --filter "FullyQualifiedName~<Feature>Tests" --logger "trx;LogFileName=<Feature>.trx" --results-directory tests/Nabadat.E2ETests/TestResults` |
| **Open a trace** | `powershell -ExecutionPolicy Bypass -File tests/Nabadat.E2ETests/bin/Debug/net10.0/playwright.ps1 show-trace <artifacts-dir>/<TestName>.zip` (or double-click the attached `<TestName>.trace.cmd`) |

Use `powershell` (Windows PowerShell), **not** `pwsh` — PowerShell 7 may not be installed.
The trace viewer runs a server until closed, so launch it in the background.

## Adding a new SPA to the merged project (the project already exists)

The `tests/Nabadat.E2ETests/` project already exists, is registered in `Nabadat.TenantAdmin.sln`, and
has its one shared harness `Infrastructure/E2ETestBase.cs` for the `frontend/` app. For the current
app you **add nothing structural** — drop your `<Feature>Tests.cs` into the right module folder and
extend `E2ETestBase`.

Only when a **new, separately authenticated SPA** is added (a second app on its own host with a
different login flow) do you add one new harness, `Infrastructure/<App>/E2ETestBase.cs` (copy
`E2ETestBase` and adapt `SignInAsync` + the session-store assertions to that app's login flow). You
do **not** create a new project, and you do **not** branch the existing harness. Add an extra
PackageReference only if that app needs one beyond what the project already carries
(`Microsoft.Playwright.MSTest`, MSTest, `OTP.NET`, `Deque.AxeCore.Playwright`, `Npgsql`).

Only if `tests/Nabadat.E2ETests/` is somehow missing entirely do you scaffold the project once (a
**standalone `net10.0` MSTest + Playwright** project — no reference to any other project):

```powershell
# 1. Create an SDK test project on a modern TFM (Playwright drives a browser over HTTP,
#    so the TFM is independent of the SPA).
dotnet new classlib -o tests/Nabadat.E2ETests -f net10.0
Remove-Item tests/Nabadat.E2ETests/Class1.cs -ErrorAction SilentlyContinue

# 2. Add the test + browser packages. MSTest (not xUnit) because Playwright ships an
#    official PageTest base for MSTest, and MSTest attachments render in VS Test Explorer.
#    OTP.NET drives MFA/TOTP login (ComputeTotp); Deque.AxeCore.Playwright + Npgsql back the
#    accessibility audit and the KPI-binding SQL seed respectively.
dotnet add tests/Nabadat.E2ETests package Microsoft.NET.Test.Sdk --version 17.*
dotnet add tests/Nabadat.E2ETests package Microsoft.Playwright.MSTest --version 1.*
dotnet add tests/Nabadat.E2ETests package MSTest.TestAdapter --version 3.*
dotnet add tests/Nabadat.E2ETests package MSTest.TestFramework --version 3.*

# 3. Register it in the solution (under a tests/ folder).
dotnet sln Nabadat.TenantAdmin.sln add tests/Nabadat.E2ETests

# 4. Build once so the Playwright tooling lands in bin/, then install the browsers.
dotnet build tests/Nabadat.E2ETests
powershell -ExecutionPolicy Bypass -File tests/Nabadat.E2ETests/bin/Debug/net10.0/playwright.ps1 install
```

Ensure the `.csproj` has `<IsPackable>false</IsPackable>` and
`<IsTestProject>true</IsTestProject>` — the `Microsoft.NET.Test.Sdk` + MSTest packages
supply the rest of the test SDK wiring. Then create the shared `Infrastructure/E2ETestBase.cs`
(below). The project is plain `net10.0` and references **no** other project — the SPA can't be built
by `dotnet`, and the tests don't need it.

### `E2ETestBase.cs` — the harness (MSTest + `Microsoft.Playwright.MSTest`)

Inherit Playwright's `PageTest`, which manages the browser/context/`Page` per test. Use
`[TestInitialize]` to start a trace and `[TestCleanup]` to write + **attach** the
screenshot and trace via `TestContext.AddResultFile` (which surfaces them in the VS Test
Explorer *Attachments* section). `TestContext.TestName` / `CurrentTestOutcome` give the
per-test name and pass/fail status.

```csharp
using Microsoft.Playwright;
using Microsoft.Playwright.MSTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nabadat.E2ETests.Infrastructure;

public abstract class E2ETestBase : PageTest
{
    // NOTE: do not declare a `TestContext` property — PageTest (WorkerAwareTest) already
    // exposes one that MSTest populates; shadowing it breaks the framework's own teardown.

    /// <summary>Base URL of the already-running portal dev server. Start the stack first
    /// (Postgres + Nabadat.TenantApp + `npm run dev`) and set $env:E2E_BASE_URL, e.g.
    /// 'http://localhost:5173'.</summary>
    protected string BaseUrl =>
        Environment.GetEnvironmentVariable("E2E_BASE_URL")?.TrimEnd('/')
        ?? throw new InvalidOperationException(
            "E2E_BASE_URL is not set. Start the portal (Postgres + Nabadat.TenantApp + " +
            "`npm run dev` in frontend/portal) and set $env:E2E_BASE_URL to the Vite dev " +
            "server URL, e.g. 'http://localhost:5173'.");

    private static string ArtifactsDir =>
        Path.Combine(AppContext.BaseDirectory, "playwright-artifacts");

    /// <summary>The Vite dev server is HTTP; the proxied backend uses a self-signed dev
    /// cert. Ignore cert errors so proxied HTTPS calls don't fail the context.</summary>
    public override BrowserNewContextOptions ContextOptions() => new() { IgnoreHTTPSErrors = true };

    /// <summary>Sign in fresh through the real, MFA-gated portal flow.
    ///
    /// The portal login is MULTI-STEP (confirm the live selectors/routes against
    /// frontend/portal/src/pages/LoginPage.tsx, MfaChallengePage.tsx, and
    /// frontend/portal/src/features/auth/api.ts):
    ///   1. /login — fill #login-email + #login-password, submit the password form.
    ///   2. The app navigates to /auth/mfa/verify (or /auth/mfa/enroll for a new factor).
    ///   3. Complete MFA: enter the TOTP code computed from the seeded factor secret.
    ///   4. On success a `session_token` is written to localStorage and the app lands on
    ///      the authenticated shell.
    ///
    /// Two strategies — pick per test suite:
    ///   (A) UI-driven (below): drives every step in the browser. Most faithful; slower.
    ///       Compute the 6-digit TOTP from the seeded secret (e.g. with Otp.NET) at step 3.
    ///   (B) FAST-PATH (recommended for suites that aren't testing the login UI itself):
    ///       authenticate once via the API (POST /api/v1/portal/auth/token then
    ///       /auth/mfa/verify with a computed TOTP), capture the returned session token,
    ///       then seed it directly:
    ///         await Page.GotoAsync(BaseUrl);
    ///         await Page.EvaluateAsync("t => localStorage.setItem('session_token', t)", token);
    ///         await Page.ReloadAsync();
    ///       This skips re-driving MFA in every test while still exercising the real app.
    ///
    /// Read the seeded username/password/TOTP-secret from appsettings.local.json (gitignored).
    /// Clears storage first so switching users mid-test works.</summary>
    protected async Task SignInAsync(string email, string password, string totpCode)
    {
        await Context.ClearCookiesAsync();
        await Page.GotoAsync($"{BaseUrl}/login");
        await Page.FillAsync("#login-email", email);
        await Page.FillAsync("#login-password", password);
        // The password submit button is the form's submit (text from i18n: auth.signInPassword).
        await Page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("(Sign in|تسجيل)") }).Last.ClickAsync();

        // MFA challenge step — confirm the code input selector against MfaChallengePage.tsx.
        await Page.WaitForURLAsync(new System.Text.RegularExpressions.Regex(".*/auth/mfa/.*"));
        await Page.GetByRole(AriaRole.Textbox).First.FillAsync(totpCode);
        await Page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("(Verify|تحقق|Continue)") }).ClickAsync();

        // Landed on the authenticated shell.
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    [TestInitialize]
    public async Task StartTracing() =>
        await Context.Tracing.StartAsync(new()
        {
            Title = TestContext.TestName,
            Screenshots = true, Snapshots = true, Sources = true,
        });

    [TestCleanup]
    public async Task CaptureArtifacts()
    {
        Directory.CreateDirectory(ArtifactsDir);
        var name = SafeName(TestContext.TestName);

        // Screenshot + trace for every test, ATTACHED to the result so they show in the VS
        // Test Explorer "Attachments" section (and the TRX/HTML report). A double-clickable
        // <test>.trace.cmd launcher opens the trace in the viewer. Open a trace manually with:
        //   powershell -File bin/Debug/net10.0/playwright.ps1 show-trace <the-trace.zip>
        var png = Path.Combine(ArtifactsDir, $"{name}.png");
        try { await Page.ScreenshotAsync(new() { Path = png, FullPage = true }); TestContext.AddResultFile(png); } catch { }

        var zip = Path.Combine(ArtifactsDir, $"{name}.zip");
        try { await Context.Tracing.StopAsync(new() { Path = zip }); TestContext.AddResultFile(zip); } catch { }

        if (File.Exists(zip))
            TestContext.AddResultFile(WriteTraceLauncher(name, Path.GetFileName(zip)));
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

    private static string SafeName(string? testName)
    {
        var safe = testName ?? "test";
        foreach (var c in Path.GetInvalidFileNameChars()) safe = safe.Replace(c, '_');
        return safe;
    }
}
```

> **REQUIRED — never drop the trace launcher.** `CaptureArtifacts` MUST write **all three**
> artifacts per test and attach each via `TestContext.AddResultFile`: the screenshot
> (`<TestName>.png`), the trace (`<TestName>.zip`), **AND** the `WriteTraceLauncher`-generated
> **`<TestName>.trace.cmd`** — a double-clickable batch launcher that runs
> `playwright.ps1 show-trace` on that test's `.zip`, i.e. the one-click path into the trace
> viewer. Carry the `WriteTraceLauncher` helper **verbatim**; a "simplified" `CaptureArtifacts`
> that emits only the png/zip and omits the `.trace.cmd` is **incomplete** and a regression
> against this lane. When scaffolding a new workspace's `E2ETestBase.cs`, copy the full
> `CaptureArtifacts` + `WriteTraceLauncher` pair below — do not trim it.

> **Skips:** MSTest has no `[Fact(Skip=…)]` equivalent for runtime decisions — use
> `Assert.Inconclusive("reason")` to skip a test when required data (e.g. a no-permission
> account, or a missing TOTP secret) is unavailable. It shows as a yellow/skipped result
> in Test Explorer.

## Workflow

### 0. Confirm sign-in credentials (ask the user; never seed)

Before anything else, resolve `E2E_USER` / `E2E_PASSWORD` (+ `E2E_TOTP_SECRET` for
MFA-gated workspaces) and `E2E_BASE_URL` per the **Credentials** section above. If any are
missing, **ASK THE USER** — do not invent, seed, or create an account. Only continue past
this gate once the credentials are provided or the user explicitly opts to run just the
no-login tests (the rest go `Assert.Inconclusive`).

Re-run this gate **after** you author the tests, too: any test may introduce a new fixture
key (a deactivated / not-yet-enrolled / non-admin account, …). Per **Parameter completeness**
above, every key your tests read must be filled in `appsettings.local.json` or explicitly
asked for — a key you silently leave undefined is a defect, not an `Assert.Inconclusive`.

### 1. Gather the sources of truth

Pull requirements from **both** the spec and the React code — they cover different things
(the spec states intent and edge rules; the code reveals the real routes, fields, and
messages).

- **Spec docs** — the feature's `specs/<feature>/spec.md` is the primary source: its user
  stories, **E2E Test Coverage** blocks, and acceptance scenarios map directly to matrix
  rows. Also `Glob` for any `**/*.md` / `**/*.pdf` under `docs/`, `specs/`, repo root if
  more context is needed (`Read` handles both directly).
- **Code** — read the routes and pages:
  - **Routes** → `frontend/portal/src/App.tsx` (every `<Route path=...>` is a navigable
    URL; note which are wrapped in `RequireAuth`).
  - **Pages / selectors** → `frontend/portal/src/pages/**.tsx` and the feature components
    in `frontend/portal/src/features/<feature>/`. Every form field, button, validation
    branch, empty state, and error message is a scenario to cover.
  - **API shape** → `frontend/portal/src/features/<feature>/api.ts` reveals the endpoints
    the page calls (useful for the fast-path sign-in and for understanding error codes).

### 2. Derive the coverage matrix → `COVERAGE.md`

Turn what you read into a table, grouped by feature. Each row is one scenario that will
become exactly one test method. Cover the obvious axes: **happy path**,
**validation/error states**, **auth & permission rules** (signed-out → redirect to
`/login`; persona without access), **empty states**, and **boundary rules** stated in the
spec. Write it to the Coverage matrix path so coverage is traceable:

```markdown
| ID    | Feature  | Source            | Scenario                                              | Test method                                | Status |
|-------|----------|-------------------|-------------------------------------------------------|--------------------------------------------|--------|
| SES-1 | Sessions | spec US3 + code   | Signed-in user sees their active sessions listed      | Active_sessions_are_listed                 | todo   |
| SES-2 | Sessions | code (RequireAuth)| Signed-out user is redirected to /login               | Signed_out_user_is_redirected_to_login     | todo   |
| LOG-1 | Login    | LoginPage.tsx     | Invalid email shows a validation message              | Invalid_email_shows_validation_message     | todo   |
```

Keep `ID` and `Test method` stable — each generated test carries its `ID` in a comment so
the matrix and the code stay in sync. Update `Status` (`todo` → `pass` / `fail`) after the
run. If you intentionally skip a row (e.g. needs data you can't seed), say so in the matrix
rather than dropping it silently.

### 3. Generate tests directly into `<Feature>Tests.cs`

One class per feature (`SessionsTests.cs`, `LoginTests.cs`, …), one test method per matrix
row, named exactly as the matrix says. Inherit `E2ETestBase` for `SignInAsync` / `BaseUrl`
/ auto screenshot + trace. **Do not create a `_Scratch.cs`** — write the real test. If you
must confirm a selector first, drop a temporary
`Console.WriteLine(await Page.Locator("body").InnerTextAsync())` *inside the test method*,
run once, then replace it with the `Expect(...)` assertion — never leave a reconnaissance
dump in a committed test. See the worked example below.

### 4. Run & read the evidence

Start the stack and set `E2E_BASE_URL` (see "Starting the stack for E2E"), then use the
**Run** command filtered to the feature. It writes a TRX **report** and, via
`E2ETestBase`, a screenshot + trace (+ `.trace.cmd` launcher) per test under the artifacts
dir — all attached to the test result, so in VS Test Explorer they appear under
**Attachments** in the selected test's detail summary.

**Surface the report and artifacts as clickable links** so the user can open them, e.g.
[Sessions.trx](tests/Nabadat.E2ETests/TestResults/Sessions.trx), plus the per-test
`[<TestName>.png](...)` and `[<TestName>.zip](...)`.

- **`<TestName>.png`** — final full-page screenshot. Read it to *see* the page.
- **`<TestName>.zip`** — full trace. **On any failure, open it before reporting** — the
  filmstrip + per-action DOM snapshots show *why* it failed; scrub to the failing action.
  (No GUI? The zip's `resources/*.jpeg` frames are Readable.)

### 5. Reconcile & report

Update each `Status` in `COVERAGE.md`, then report the matrix back: how many rows covered,
which pass, which fail (with the real reason from the trace — a genuine app bug is a
finding, not something to mask by weakening the assertion).

## The generated test — worked example

The matrix rows become test methods in `<Feature>Tests.cs`. Each carries its matrix `ID`,
has no `_Scratch` prefix, and uses real `Expect(...)` / `Assert` assertions (one scenario
per method). **Adapt the routes, fields, button text, and accounts to this repo** by
reading `App.tsx`, the page `.tsx` files, and the seed data:

```csharp
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nabadat.E2ETests.Infrastructure;

namespace Nabadat.E2ETests.KpiManagement;

[TestClass]
public class SessionsTests : E2ETestBase
{
    // Covers SES-1: a signed-in user sees their active sessions in the list.
    [TestMethod]
    public async Task Active_sessions_are_listed()
    {
        await SignInAsync("<seeded-user@tenant>", "<password>", ComputeTotp());  // discover real account + secret
        await Page.GotoAsync($"{BaseUrl}/auth/sessions");                         // route from App.tsx
        await Expect(Page.GetByRole(AriaRole.Heading)).ToBeVisibleAsync();
        await Expect(Page.Locator("tbody tr").First).ToBeVisibleAsync();          // discover real list selector
    }

    // Covers SES-2: a signed-out user hitting a protected route is redirected to /login.
    [TestMethod]
    public async Task Signed_out_user_is_redirected_to_login()
    {
        await Context.ClearCookiesAsync();
        await Page.GotoAsync($"{BaseUrl}/auth/sessions");   // RequireAuth wraps this route
        await Expect(Page).ToHaveURLAsync(new Regex(".*/login"));
    }

    private static string ComputeTotp() => /* compute from the seeded secret in appsettings.local.json */ "000000";
}
```

`Expect` is provided by Playwright's `PageTest` base class (inherited via `E2ETestBase`),
so call `Expect(...)` directly — no `using static` needed. Use
`StringAssert.Matches(value, regex)` for plain-string regex assertions. Run with the
**Run** command filtered to `SessionsTests`.

## Reconnaissance-then-action

Don't guess selectors. Confirm the element exists *before* asserting on it — read the page
`.tsx` (the source of truth for field `id`s, `aria-label`s, and button text), and if still
unsure, dump the rendered DOM once from *inside the test method* (then replace the dump
with the assertion). Prefer role/text/id locators over brittle CSS:

```csharp
await Page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
await Expect(Page.GetByText("saved")).ToBeVisibleAsync();
var row = Page.Locator("tbody tr").Filter(new() { HasText = "Acme" });
```

**Bilingual / RTL caution.** The portal renders in **Arabic and English** (i18next) with
**RTL and LTR** directions. Text labels change with language, so prefer **stable `id`s**
(`#login-email`), **`getByRole`**, or **`data-testid`** over `getByText("…")`. If you must
assert on visible copy, pin the language first (set the i18n language / `localStorage` lang
key, or click the language toggle) so the test is deterministic. Where useful, cover both
directions as separate matrix rows.

## Accounts & permissions (discover, don't assume)

The portal authenticates against the **backend** (real users, MFA-gated, persona/permission
scopes per constitution Section 9 `P-01`…`P-08`). There is **no fixed demo-account table**
to copy — find valid credentials and the persona/permission chain from the seed data and
the auth code:

- Login + MFA flow: `frontend/portal/src/pages/LoginPage.tsx`,
  `MfaChallengePage.tsx`, and `frontend/portal/src/features/auth/api.ts`.
- Permission/persona gating: the route guards in `App.tsx` (`RequireAuth`) and the
  backend's permission middleware (owned by M-10).
- Test accounts + TOTP secret: ask the user, or read the backend seed scripts. Put them in
  `appsettings.local.json` (gitignored; copy `appsettings.local.example.json`). Note that
  E2E writes are **real DB rows** (no transaction rollback here, unlike the backend
  integration lane) — prefer read-only/idempotent flows, use **distinct unique values**
  per run for anything uniqueness-constrained, and clean up or accept the residue. Call out
  any test that mutates shared state.

> Unlike the backend integration lane, **E2E tests are not wrapped in a rolled-back
> transaction** — they exercise the full stack against the running app, so their writes
> persist. Design scenarios accordingly (unique inputs, cleanup, or read-only assertions).

## Discovering routes & selectors

**Don't hardcode a route/selector table — it rots as the app changes.** Read them from the
source, the single point of truth:

- **Routes** → `frontend/portal/src/App.tsx` (every `<Route path="…">`; note `RequireAuth`
  wrappers for auth-gated routes).
- **Selectors / field names / button text** → the page in
  `frontend/portal/src/pages/<Page>.tsx` and its feature components under
  `frontend/portal/src/features/<feature>/` (and shared `components/layout/`).
- **i18n copy** → `frontend/portal/src/i18n/locales/{ar,en}.json` if you must assert on
  text (but prefer `id`/`role` — see the bilingual caution above).
- **At runtime**, the reconnaissance dump confirms what's actually rendered — trust that
  over any written list.

The first `<Feature>Tests.cs` you write becomes the most up-to-date example of real
selectors in use — copy from it thereafter.

## How this lane plugs into spec-kit

This skill is the authoring engine; the spec-kit flow decides *when* it runs:

- **`/speckit-specify`** populates an **E2E Test Coverage** block in each page-bearing
  frontend user story (or `e2e-tests: skipped — <reason>`). That block is the spec-level
  source for your matrix rows.
- **`/speckit-tasks`** emits an **E2E Tests for User Story X 🎭** subsection — placed
  *after* the implementation tasks (the pages must exist first) and *before* the per-story
  checkpoint — citing `tests/Nabadat.E2ETests/<Module>/<Feature>Tests.cs` for the story's
  module/workspace. There is **no red checkpoint** for E2E (they exercise existing pages, like
  integration tests).
- **`/speckit-implement`** runs the E2E suite at the **per-story checkpoint**: it starts
  the stack, sets `E2E_BASE_URL`, ensures browsers are installed, and runs the filtered
  `dotnet test`. The frontend story's build gate is `npm run build` **AND** the E2E filter
  green.

See project-root `CLAUDE.md` → "E2E Test Policy" for the binding rules.

## Pitfalls that have bitten this lane (read before authoring assertions)

These are real failures this lane has hit. Each is a test-authoring bug, not an app bug —
avoid them up front.

1. **Strict-mode: a Playwright locator must resolve to exactly ONE element when you act/assert.**
   `Expect(Page.GetByText(new Regex(@"\d+"))).ToBeVisibleAsync()` matched **12** elements (every
   GUID prefix, persona code, etc.) and threw a *strict mode violation*. Never assert on a
   page-wide text/regex. **Scope** the locator (within a dialog, row, region) or assert a value,
   not a pattern: read the dialog message and `StringAssert.Matches(msg, …)`. When you genuinely
   expect several matches, use `.First`/`.Nth(i)` deliberately — never by accident.

2. **Substring label matches collide with sibling controls.** A regex `"(Reactivate|إعادة)"`
   also matched **"إعادة تعيين التحقق"** (reset-MFA) and **"إعادة تعيين كلمة المرور"**
   (reset-password) — both start with "إعادة". Match the **full** label
   (`"(Reactivate|إعادة تفعيل)"`) or use a stable `id`/`role`. Check the page `.tsx` for *all*
   buttons whose text shares your prefix before picking a regex.

3. **Native `window.confirm` / `alert` / `prompt` are NOT DOM — they're browser dialogs.** A
   `GetByText`/`GetByRole` locator will never find them (and the action *hangs* until the dialog
   is handled). Register a handler **before** the click and accept/dismiss it; the confirmation
   text (e.g. an interpolated affected-count) is on `dialog.Message`:
   ```csharp
   string? msg = null;
   Page.Dialog += async (_, d) => { msg = d.Message; await d.DismissAsync(); }; // Dismiss = don't proceed
   await deleteButton.ClickAsync();
   StringAssert.Matches(msg!, new Regex(@"\d+"));
   ```
   Use `AcceptAsync()` when the test *should* proceed (e.g. confirm a deactivate), `DismissAsync()`
   when it should not (e.g. assert the prompt without actually deleting). Grep the page for
   `window.confirm(` / `confirm(` to know which actions are native.

4. **Native `<select>` (shadcn `Select`) is driven by `SelectOptionAsync`, not `.ClickAsync()` on
   the value text.** Clicking the displayed value does nothing. Also mind **disabled-submit**
   guards: a "save" button gated on `value !== current` stays disabled until you pick a
   *different* option — read the current value first, then select another.

5. **Don't commit stub/unconfirmed bodies.** The original failures included a test that *opened* a
   dialog but never filled or submitted it (a `// ... fill here ...` placeholder), and tests whose
   selectors were "to be confirmed at runtime" but never were. **Every generated test must be RUN
   against the live app and pass (or expose a genuine app bug) before you consider it done** —
   reconciling `COVERAGE.md` to `pass` on a test you didn't run is the root cause of most of these.

6. **E2E writes persist — mutating tests must self-heal, not assume a pristine start.** A
   deactivate→reactivate test left its target **deactivated** when an earlier run failed
   mid-flow; the next run then timed out waiting for the now-absent "Deactivate" button. For any
   test that flips state, **establish the precondition defensively** (e.g. "if a Reactivate button
   is present, click it first to return to the active state") so a prior interrupted run can't
   wedge it. Prefer a disposable target over a meaningful account, keep uniqueness-constrained
   inputs unique per run, and call out shared-state mutation in `COVERAGE.md`.

7. **A bounce back to `/login` right after sign-in is usually infra, not the login UI.** Before
   "fixing" the TOTP/login steps, check the trace network tab for `307`/`401` on `/api/...` — see
   the proxy/redirect caution near the top. Anonymous login/TOTP calls succeeding while the first
   authenticated call 401s is the tell.

## Best practices

- One scenario per test method; one feature per class; keep each method small.
- Every test traces back to a `COVERAGE.md` row (carry the `ID` in a comment); keep the
  matrix `Status` current after each run.
- Sign in via `SignInAsync` — it clears storage first, so switching users mid-test works.
- Prefer stable `id`/`role`/`data-testid` over translated text (bilingual app); pin the
  language when asserting on copy.
- Read the screenshot/trace before concluding "it works" — don't infer UI state from logs.
- No `_Scratch.cs`, and no leftover DOM-dump `Console.WriteLine` in committed tests —
  generate the real `<Feature>Tests.cs` with assertions.
- A failing test that reflects a real app bug is a finding to report — don't weaken the
  assertion to force green.
- Remember E2E writes persist (no rollback) — keep inputs unique and clean up shared state.
- Every locator you act on must resolve to exactly one element; never commit a test you
  haven't run green against the live app. See **Pitfalls that have bitten this lane** above.
- Fill EVERY config key your tests read (not just the login triplet) — deactivated /
  not-yet-enrolled / non-admin fixtures each get their own key, listed in
  `appsettings.local.example.json`. A key you can't fill → ASK; never leave it undefined or
  paper over it with `Assert.Inconclusive`. See **Parameter completeness** above.
- Derive `E2E_BASE_URL`'s scheme from the workspace's `vite.config.ts` (`server.https`), not
  from a table — `ERR_SSL_PROTOCOL_ERROR` on the first `GotoAsync` means the scheme is
  inverted. On the tenant-app, run the backend's **`http`** launch profile to avoid the 307
  auth-strip. See the two **⚠️** cautions near the top.
