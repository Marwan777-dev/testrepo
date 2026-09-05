namespace Nabadat.UserManagement.Api.Tenancy;

/// <summary>
/// Resolves a tenant slug to its metadata. The slug→tenant mapping that AD-07 needs
/// at the edge (before a tenant connection exists). Config-backed today; swap for an
/// M-11 control-plane <c>tenants</c> lookup when provisioning lands.
/// </summary>
public interface ITenantRegistry
{
    /// <summary>Returns the tenant for <paramref name="slug"/>, or <c>false</c> if unknown.</summary>
    bool TryResolve(string slug, out TenantInfo info);

    /// <summary>All registered tenants, keyed by slug. Used by the dev seeder/provisioner.</summary>
    IReadOnlyDictionary<string, TenantInfo> All { get; }
}
