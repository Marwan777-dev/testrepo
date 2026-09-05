using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.ValueObjects;
using Nabadat.UserManagement.Application.Interfaces;

namespace Nabadat.UserManagement.Application.Users.Interfaces;

/// <summary>
/// Context-holding data-access service over <c>tenant_users</c> (EF Core /
/// <c>TenantDbContext</c>), replacing the raw-Npgsql <c>ITenantUserRepository</c>.
/// Write methods persist immediately (their own <c>SaveChangesAsync</c>); to commit
/// several writes atomically, compose them inside <c>ITenantDbContext.ExecuteAsync</c>,
/// whose transaction governs commit/rollback.
/// </summary>
public interface ITenantUserService
{
    Task<TenantUser?> GetByUsernameAsync(string username, CancellationToken ct = default);

    Task<TenantUser?> GetByIdAsync(Guid userId, CancellationToken ct = default);

    Task<bool> ExistsAsync(string username, CancellationToken ct = default);

    /// <summary>Inserts the user and saves.</summary>
    Task AddAsync(TenantUser user, CancellationToken ct = default);

    /// <summary>Updates the user and saves; safe whether the entity is tracked or detached.</summary>
    Task UpdateAsync(TenantUser user, CancellationToken ct = default);

    /// <summary>
    /// Cursor-paginated user list (API-04), filtered by status/persona/username search and
    /// ordered by the stable <c>(created_at, user_id)</c> keyset.
    /// </summary>
    Task<UserListPage> ListAsync(
        string? status,
        string? persona,
        string? search,
        int pageSize,
        string? pageToken,
        CancellationToken ct = default);
}
