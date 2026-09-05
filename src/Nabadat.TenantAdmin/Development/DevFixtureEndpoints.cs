namespace Nabadat.TenantAdmin.Development;

/// <summary>
/// Development-only HTTP fixtures that let the browser-E2E suite reset specific
/// seeded accounts to a known state WITHOUT a backend restart. Mapped only when the
/// host runs in the Development environment (gated in <c>Program.cs</c>). Each
/// endpoint targets a single fixture account, so the tests stay parallel-safe.
/// </summary>
public static class DevFixtureEndpoints
{
    public static IEndpointRouteBuilder MapDevFixtureEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Re-seed the AUTH-2 enrollment account back to its pending-enrollment state
        // (de-enrolled MFA, cleared lockout/attempt state) by re-running the seeder's
        // enroll-user upsert. The enrollment test permanently enrolls this user, so it
        // calls this first to stay re-runnable between backend restarts.
        endpoints.MapPost("/api/v1/dev/fixtures/reseed-enroll",
            async (IServiceProvider services, CancellationToken ct) =>
            {
                await DevDataSeeder.SeedEnrollUserAsync(services, ct);
                return Results.Ok(new { reseeded = DevDataSeeder.EnrollEmail });
            });

        return endpoints;
    }
}
