namespace Nabadat.UserManagement.Infrastructure.Crypto;

/// <summary>
/// SaaS (AWS) <see cref="IMfaSecretEncryptionService"/>. Selected when
/// <c>ENABLE_MULTI_TENANT</c> is true and the cloud provider is AWS. Phase 1 ships
/// the on-prem <see cref="LocalAesEncryptionService"/> as the working path; the
/// KMS envelope flow (GenerateDataKey + local AES under the data key) is wired
/// here but not yet implemented.
/// </summary>
public sealed class AwsKmsEncryptionService : IMfaSecretEncryptionService
{
    public Task<EncryptedSecret> EncryptAsync(string plainSecret, CancellationToken ct = default) =>
        throw new NotImplementedException("AWS KMS envelope encryption is not implemented in Phase 1.");

    public Task<string> DecryptAsync(byte[] cipher, string keyRef, CancellationToken ct = default) =>
        throw new NotImplementedException("AWS KMS envelope decryption is not implemented in Phase 1.");
}
