using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Nabadat.UserManagement.Infrastructure.Crypto;

/// <summary>
/// On-premises <see cref="IMfaSecretEncryptionService"/> (selected when
/// <c>ENABLE_MULTI_TENANT</c> is false). Encrypts with AES-256-GCM under a
/// 256-bit key read from the <c>MfaEncryptionKey</c> configuration value
/// (Base64-encoded; supplied via env var or secret store, never committed).
///
/// Cipher layout: <c>nonce(12) ‖ tag(16) ‖ ciphertext</c>.
/// </summary>
public sealed class LocalAesEncryptionService : IMfaSecretEncryptionService
{
    private const string KeyConfigName = "MfaEncryptionKey";
    private const string KeyReference = "local:config:MfaEncryptionKey";
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    public LocalAesEncryptionService(IConfiguration configuration)
    {
        var configured = configuration[KeyConfigName];
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                $"'{KeyConfigName}' is not configured. On-premises MFA secret encryption requires a Base64 256-bit key.");
        }

        _key = Convert.FromBase64String(configured);
        if (_key.Length != 32)
        {
            throw new InvalidOperationException(
                $"'{KeyConfigName}' must decode to a 256-bit (32-byte) key; got {_key.Length} bytes.");
        }
    }

    public Task<EncryptedSecret> EncryptAsync(string plainSecret, CancellationToken ct = default)
    {
        var plaintext = Encoding.UTF8.GetBytes(plainSecret);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using (var aes = new AesGcm(_key, TagSize))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        var packed = new byte[NonceSize + TagSize + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, packed, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, packed, NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, packed, NonceSize + TagSize, ciphertext.Length);

        return Task.FromResult(new EncryptedSecret { Cipher = packed, KeyRef = KeyReference });
    }

    public Task<string> DecryptAsync(byte[] cipher, string keyRef, CancellationToken ct = default)
    {
        if (cipher.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("Cipher payload is too short to contain a nonce and tag.");
        }

        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var ciphertext = new byte[cipher.Length - NonceSize - TagSize];
        Buffer.BlockCopy(cipher, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(cipher, NonceSize, tag, 0, TagSize);
        Buffer.BlockCopy(cipher, NonceSize + TagSize, ciphertext, 0, ciphertext.Length);

        var plaintext = new byte[ciphertext.Length];
        using (var aes = new AesGcm(_key, TagSize))
        {
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
        }

        return Task.FromResult(Encoding.UTF8.GetString(plaintext));
    }
}
