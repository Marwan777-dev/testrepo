using Nabadat.IntegrationHub.Domain.ValueObjects;

namespace Nabadat.IntegrationHub.Domain.Entities;

/// <summary>
/// A caller-authentication secret for one <see cref="Integration"/> (data-model.md §2). At most one
/// row per integration is <see cref="CredentialStatus.Active"/> at a time: generating a new credential
/// implicitly revokes the prior Active one in a <b>single atomic write</b> (BR-16), not two user
/// actions. Revoked rows are retained for audit and never deleted.
///
/// <para><b>Fixed in code, never columns</b> (ratified removals — <c>[PO-G13]</c>, BR-17): grant type
/// (always <c>client_credentials</c>), access-token lifetime (always 15 minutes), expiry, sandbox
/// flag, and allowed-source-IPs. <c>CredentialFieldSetGuard</c> (US8) pins their absence.</para>
/// </summary>
public sealed class Credential
{
    public Guid Id { get; set; }

    /// <summary>Intra-module FK → <see cref="Integration"/>.</summary>
    public Guid IntegrationId { get; set; }

    /// <summary>Discriminator selecting the field set below; fixed at first generation.</summary>
    public CredentialMechanism Mechanism { get; set; }

    /// <summary>The API key's <c>keyLabel</c> or the OAuth client's <c>clientName</c> — required (VR-F10).</summary>
    public string LabelOrClientName { get; set; } = string.Empty;

    /// <summary>
    /// Hashed/encrypted at rest (BR-16, NFR-6). <b>Never</b> the plaintext: the secret is returned
    /// exactly once, in the show-once dialog at generation, and is never persisted or logged.
    /// </summary>
    public string SecretHash { get; set; } = string.Empty;

    /// <summary>
    /// OAuth scopes — populated only when <see cref="Mechanism"/> is
    /// <see cref="CredentialMechanism.OAuthClient"/>; a subset of the five ratified scopes (BR-26):
    /// <c>survey-requests:write</c>, <c>survey-links:read</c>, <c>survey-definitions:read</c>,
    /// <c>survey-embed:read</c>, <c>responses:write</c>.
    /// </summary>
    public string[]? Scopes { get; set; }

    /// <summary>One-way: Active → Revoked. There is no un-revoke.</summary>
    public CredentialStatus Status { get; set; } = CredentialStatus.Active;

    public DateTimeOffset GeneratedAt { get; set; }

    /// <summary>M-10 user id — audit attribution only.</summary>
    public Guid? GeneratedBy { get; set; }

    /// <summary>Set when the credential is revoked, whether standalone or superseded by a new one.</summary>
    public DateTimeOffset? RevokedAt { get; set; }
}
