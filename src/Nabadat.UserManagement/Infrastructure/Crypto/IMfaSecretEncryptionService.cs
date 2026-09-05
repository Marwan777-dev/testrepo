namespace Nabadat.UserManagement.Infrastructure.Crypto;

/// <summary>
/// Envelope encryption for the TOTP secret (GP-02 — customer-controlled
/// encryption of high-sensitivity fields). The concrete implementation is chosen
/// by deployment mode (<c>ENABLE_MULTI_TENANT</c>): KMS-backed in SaaS,
/// config-key-backed AES on-prem. The plaintext secret is never persisted.
/// </summary>
public interface IMfaSecretEncryptionService
{
    /// <summary>Encrypts a plaintext TOTP secret, returning cipher + key reference.</summary>
    Task<EncryptedSecret> EncryptAsync(string plainSecret, CancellationToken ct = default);

    /// <summary>Decrypts a previously-encrypted secret using its key reference.</summary>
    Task<string> DecryptAsync(byte[] cipher, string keyRef, CancellationToken ct = default);
}
