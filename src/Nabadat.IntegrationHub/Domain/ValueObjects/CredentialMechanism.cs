namespace Nabadat.IntegrationHub.Domain.ValueObjects;

/// <summary>
/// How a caller authenticates against an integration's inbound endpoint (data-model.md §2). Acts as
/// the <see cref="Entities.Credential"/> discriminator: it selects which field set the credential
/// carries (label vs. client name + scopes) and is fixed at first generation.
/// <para>Persisted as <c>api_key</c> / <c>oauth_client</c> via <c>CredentialMechanismConverter</c>.</para>
/// </summary>
public enum CredentialMechanism
{
    /// <summary>A tenant-generated API key, shown once in plaintext and stored only as a hash (BR-16).</summary>
    ApiKey = 1,

    /// <summary>
    /// An OAuth 2.0 client-credentials client. Grant type (<c>client_credentials</c>) and access-token
    /// lifetime (15 minutes) are fixed in code, never columns (<c>[PO-G13]</c>, BR-17).
    /// </summary>
    OAuthClient = 2,
}
