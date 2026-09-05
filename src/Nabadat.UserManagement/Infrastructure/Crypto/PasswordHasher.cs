namespace Nabadat.UserManagement.Infrastructure.Crypto;

/// <summary>
/// bcrypt-backed <see cref="IPasswordHasher"/> (BCrypt.Net-Next) fixed at work
/// factor 12. Each <see cref="Hash"/> uses a fresh random salt, so identical
/// inputs produce distinct outputs.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string plain) =>
        BCrypt.Net.BCrypt.HashPassword(plain, WorkFactor);

    public bool Verify(string plain, string hash) =>
        BCrypt.Net.BCrypt.Verify(plain, hash);
}
