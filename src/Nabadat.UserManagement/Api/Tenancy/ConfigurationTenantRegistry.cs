using Microsoft.Extensions.Configuration;

namespace Nabadat.UserManagement.Api.Tenancy;

/// <summary>
/// <see cref="ITenantRegistry"/> backed by the <c>Tenants</c> configuration section:
/// <code>
/// "Tenants": {
///   "acme":   { "Id": "…", "DisplayName": "Acme Corp" },
///   "globex": { "Id": "…", "DisplayName": "Globex Inc" }
/// }
/// </code>
/// Slugs are matched case-insensitively. Singleton — the map is fixed for the host's
/// lifetime (a config-based stand-in for the M-11 control-plane <c>tenants</c> table).
/// </summary>
public sealed class ConfigurationTenantRegistry : ITenantRegistry
{
    public const string SectionName = "Tenants";

    private readonly Dictionary<string, TenantInfo> _tenants;

    public ConfigurationTenantRegistry(IConfiguration configuration)
    {
        var bound = configuration.GetSection(SectionName).Get<Dictionary<string, TenantInfo>>()
            ?? new Dictionary<string, TenantInfo>();

        _tenants = new Dictionary<string, TenantInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var (slug, info) in bound)
        {
            _tenants[slug.Trim().ToLowerInvariant()] = info;
        }
    }

    public IReadOnlyDictionary<string, TenantInfo> All => _tenants;

    public bool TryResolve(string slug, out TenantInfo info) => _tenants.TryGetValue(slug, out info!);
}
