namespace Nabadat.KpiManagement.IntegrationTests.Infrastructure;

/// <summary>
/// Produces an authenticated <see cref="HttpClient"/> for a given persona, the seam the M-06
/// endpoint/scenario tests use to exercise the persona authority model (P-01 authors KPIs; P-02 is
/// read-only; P-06 is an executive viewer; P-07 is a non-CX admin used for negative permission
/// assertions).
///
/// <para><b>Note on the token model.</b> The feature spec described persona context as "JWT
/// issuance with the M-10 signing key", but the platform actually authenticates with M-10's
/// <c>PortalSessionAdmin</c> <b>opaque session tokens</b> (CLAUDE.md §Backend Integration), not
/// JWTs. So a persona context is realized by seeding a persona user and driving the real
/// login → MFA-verify flow via <see cref="KpiManagementApplicationFactory.SignedInClientAsync"/> —
/// there is no separate JWT to mint. This helper centralizes the persona constants and that call.</para>
/// </summary>
public static class PersonaContextHelper
{
    public const string CxProgramManager = "P-01";
    public const string CxAnalyst = "P-02";
    public const string ExecutiveSponsor = "P-06";
    public const string TenantItAdministrator = "P-07";

    /// <summary>Seeds a user with <paramref name="persona"/> and returns a bearer-authenticated client.</summary>
    public static Task<HttpClient> SignedInAsAsync(this KpiManagementApplicationFactory factory, string persona) =>
        factory.SignedInClientAsync(persona);

    /// <summary>Seeds a persona user and returns both the client and the seeded actor (for actor_id assertions).</summary>
    public static Task<(HttpClient Client, SeededUser Actor)> SignedInWithActorAsAsync(
        this KpiManagementApplicationFactory factory, string persona) =>
        factory.SignedInWithActorAsync(persona);
}
