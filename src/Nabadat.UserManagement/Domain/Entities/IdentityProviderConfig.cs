namespace Nabadat.UserManagement.Domain.Entities;

/// <summary>
/// Per-tenant SSO provider configuration (control-plane table
/// <c>identity_provider_configs</c>). Forward-compatible (FR-004/FR-018): the
/// record exists so providers can be configured later, but <b>no provider logic
/// runs in Phase 1</b>. Control-plane entity — carries explicit <see cref="TenantId"/>.
/// </summary>
public sealed class IdentityProviderConfig
{
    public Guid ProviderId { get; set; }

    public Guid TenantId { get; set; }

    public IdentityProviderType ProviderType { get; set; }

    /// <summary>Provider-specific config (extensible jsonb; no hardcoded fields).</summary>
    public IReadOnlyDictionary<string, object?> Settings { get; set; }
        = new Dictionary<string, object?>();

    /// <summary>Only one provider may be active per tenant.</summary>
    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
