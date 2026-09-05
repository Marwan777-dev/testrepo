namespace Nabadat.UserManagement.Infrastructure.Crypto;

/// <summary>
/// SaaS (Azure) <see cref="IMfaSecretEncryptionService"/>. Selected when
/// <c>ENABLE_MULTI_TENANT</c> is true and the cloud provider is Azure. Phase 1
/// ships the on-prem <see cref="LocalAesEncryptionService"/> as the working path;
/// the Key Vault envelope flow is wired here but not yet implemented.
/// </summary>
public sealed class AzureKmsEncryptionService : IMfaSecretEncryptionService
{
    public Task<EncryptedSecret> EncryptAsync(string plainSecret, CancellationToken ct = default) =>
        throw new NotImplementedException("Azure Key Vault envelope encryption is not implemented in Phase 1.");

    public Task<string> DecryptAsync(byte[] cipher, string keyRef, CancellationToken ct = default) =>
        throw new NotImplementedException("Azure Key Vault envelope decryption is not implemented in Phase 1.");
}
