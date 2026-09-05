namespace Nabadat.UserManagement.Infrastructure.Crypto;

/// <summary>One-way password hashing (bcrypt, cost ≥ 12 per security constitution Article 2.1).</summary>
public interface IPasswordHasher
{
    /// <summary>Hashes a plaintext password. Output fits the <c>varchar(72)</c> column.</summary>
    string Hash(string plain);

    /// <summary>Verifies a plaintext password against a stored bcrypt hash.</summary>
    bool Verify(string plain, string hash);
}
