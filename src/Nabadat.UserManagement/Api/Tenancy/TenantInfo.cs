namespace Nabadat.UserManagement.Api.Tenancy;

/// <summary>
/// Registry entry for one tenant: its control-plane id and display name. Bound from
/// the <c>Tenants</c> configuration section (keyed by slug). A stand-in for the M-11
/// <c>tenants</c> control-plane table, which is not present in this repo yet.
/// </summary>
public sealed class TenantInfo
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;
}
