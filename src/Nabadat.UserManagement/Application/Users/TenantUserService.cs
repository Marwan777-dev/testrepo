using Microsoft.EntityFrameworkCore;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.Interfaces;
using Nabadat.UserManagement.Domain.ValueObjects;
using Nabadat.UserManagement.Application.Users.Interfaces;
using Nabadat.UserManagement.Application.Interfaces;

namespace Nabadat.UserManagement.Application.Users;

/// <summary>
/// EF <see cref="ITenantUserService"/> over <see cref="ITenantDbContext"/>. <c>GetByIdAsync</c>
/// tracks (for read-then-update flows); <c>GetByUsernameAsync</c> is no-tracking. Write
/// methods persist immediately; an <c>ITenantDbContext.ExecuteAsync</c> transaction makes multiple
/// writes atomic.
/// </summary>
public sealed class TenantUserService : ITenantUserService
{
    private readonly ITenantDbContext _context;

    public TenantUserService(ITenantDbContext context) => _context = context;

    public async Task<TenantUser?> GetByUsernameAsync(string username, CancellationToken ct = default) =>
        // No-tracking: callers read this for credential checks / existence, never to mutate
        // the returned entity (mutating loads go through GetByIdAsync, which tracks).
        await _context.TenantUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username, ct);

    public async Task<TenantUser?> GetByIdAsync(Guid userId, CancellationToken ct = default) =>
        await _context.TenantUsers.FirstOrDefaultAsync(u => u.UserId == userId, ct);

    public async Task<bool> ExistsAsync(string username, CancellationToken ct = default) =>
        await _context.TenantUsers.AnyAsync(u => u.Username == username, ct);

    public async Task AddAsync(TenantUser user, CancellationToken ct = default)
    {
        _context.TenantUsers.Add(user);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(TenantUser user, CancellationToken ct = default)
    {
        // Update() handles both the tracked case and a detached entity (attach as Modified).
        _context.TenantUsers.Update(user);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<UserListPage> ListAsync(
        string? status,
        string? persona,
        string? search,
        int pageSize,
        string? pageToken,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _context.TenantUsers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status) && UserStatusExtensions.TryParseStatus(status, out var parsedStatus))
        {
            query = query.Where(u => u.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(persona))
        {
            query = query.Where(u => u.Persona == persona);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            query = query.Where(u => EF.Functions.ILike(u.Username, pattern));
        }

        var total = await query.CountAsync(ct);

        // Keyset on the stable (created_at, user_id) ordering. The Guid tiebreaker is
        // compared as text — the canonical uuid form sorts identically to Postgres's uuid
        // ordering — so the whole predicate stays translatable LINQ (no raw SQL).
        if (TryDecodeCursor(pageToken, out var afterCreatedAt, out var afterUserId))
        {
            var afterUserIdText = afterUserId.ToString();
            query = query.Where(u =>
                u.CreatedAt > afterCreatedAt
                || (u.CreatedAt == afterCreatedAt
                    && string.Compare(u.UserId.ToString(), afterUserIdText) > 0));
        }

        var items = await query
            .OrderBy(u => u.CreatedAt)
            .ThenBy(u => u.UserId)
            .Take(pageSize + 1)
            .ToListAsync(ct);

        string? nextToken = null;
        if (items.Count > pageSize)
        {
            var last = items[pageSize - 1];
            items.RemoveAt(items.Count - 1);
            nextToken = EncodeCursor(last.CreatedAt, last.UserId);
        }

        return new UserListPage { Items = items, NextPageToken = nextToken, TotalCount = total };
    }

    private static string EncodeCursor(DateTimeOffset createdAt, Guid userId) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{createdAt.UtcTicks}:{userId}"))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool TryDecodeCursor(string? token, out DateTimeOffset createdAt, out Guid userId)
    {
        createdAt = default;
        userId = default;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            var padded = token.Replace('-', '+').Replace('_', '/').PadRight((token.Length + 3) / 4 * 4, '=');
            var parts = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded)).Split(':', 2);
            createdAt = new DateTimeOffset(long.Parse(parts[0]), TimeSpan.Zero);
            userId = Guid.Parse(parts[1]);
            return true;
        }
        catch
        {
            return false; // A malformed cursor is treated as "from the start".
        }
    }
}
