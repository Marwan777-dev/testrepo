namespace Nabadat.UserManagement.Infrastructure.Crypto;

/// <summary>
/// An envelope-encrypted secret: the <see cref="Cipher"/> bytes (stored in
/// <c>tenant_users.mfa_secret_encrypted</c>) plus the <see cref="KeyRef"/> needed
/// to decrypt them (stored in <c>tenant_users.mfa_secret_key_ref</c>).
/// </summary>
public sealed record EncryptedSecret
{
    public required byte[] Cipher { get; init; }

    public required string KeyRef { get; init; }
}
