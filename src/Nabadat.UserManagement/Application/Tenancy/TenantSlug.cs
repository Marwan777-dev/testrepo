using System.Text.RegularExpressions;

namespace Nabadat.UserManagement.Application.Tenancy;

/// <summary>
/// The slug→schema naming convention and its safety gate. A tenant's URL slug names its
/// PostgreSQL schema as <c>tenant_{slug}</c>; because that name is interpolated into a
/// <c>SET search_path</c> statement (and into DDL by the dev bootstrapper), the slug MUST
/// be validated before it ever reaches SQL — an unvalidated slug is a search-path
/// injection vector (GP-04). An Application-layer policy so every consumer — the request
/// edge (<c>TenantResolutionMiddleware</c>), the persistence interceptor
/// (<c>TenantSchemaConnectionInterceptor</c>), and the dev provisioner — depends on it
/// inward and they all agree on what a valid slug and its schema are.
/// </summary>
public static partial class TenantSlug
{
    /// <summary>
    /// A lowercase DNS-label: 1–63 chars of <c>a-z 0-9 -</c>, no leading/trailing hyphen.
    /// Slugs are canonicalised to lowercase at the edge, so uppercase is rejected here.
    /// </summary>
    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();

    public static bool IsValid(string slug) => !string.IsNullOrEmpty(slug) && SlugPattern().IsMatch(slug);

    /// <summary>The schema a valid slug maps to: <c>acme</c> → <c>tenant_acme</c>.</summary>
    public static string SchemaName(string slug) => $"tenant_{slug}";
}
