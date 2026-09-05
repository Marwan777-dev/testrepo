using System.Text;
using Microsoft.EntityFrameworkCore;
using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.Interfaces;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;

namespace Nabadat.CustomerJourneyManagement.Application.Journeys;

/// <summary>
/// EF <see cref="IJourneyDataService"/> over <see cref="ITenantDbContext"/> for the tenant-schema
/// <c>journeys</c> table. Replaces the raw-Npgsql <c>JourneyRepository</c>: reads use
/// <c>AsNoTracking</c>; <see cref="CreateAsync"/>/<see cref="UpdateAsync"/> track the change and
/// save — when the caller runs them inside <see cref="ITenantDbContext.ExecuteAsync(Func{Task}, CancellationToken)"/>
/// the row and its M-17 event commit atomically (FR-015). All queries are schema-relative
/// (DB-02/AD-02 — the tenant schema is bound per connection by the search-path interceptor). The
/// list query is keyset-paginated (API-04) over <c>(created_at, journey_id)</c> with an opaque
/// Base64 cursor.
/// </summary>
public sealed class JourneyDataService : IJourneyDataService
{
    private readonly ITenantDbContext _context;

    public JourneyDataService(ITenantDbContext context) => _context = context;

    /// <inheritdoc />
    public Task<Journey?> GetByIdAsync(Guid journeyId, CancellationToken ct = default) =>
        _context.Journeys.AsNoTracking().FirstOrDefaultAsync(j => j.JourneyId == journeyId, ct);

    /// <inheritdoc />
    public async Task<RepositoryPage<Journey>> ListAsync(
        string? status,
        int pageSize,
        string? pageToken,
        CancellationToken ct = default)
    {
        var limit = Math.Clamp(pageSize, 1, 200);
        var statusFilter = string.IsNullOrWhiteSpace(status) ? null : status;

        var hasCursor = !string.IsNullOrEmpty(pageToken);
        var cursorCreatedAt = default(DateTimeOffset);
        var cursorJourneyId = Guid.Empty;
        if (hasCursor)
        {
            if (!TryDecodeCursor(pageToken!, out var ticks, out cursorJourneyId))
            {
                throw new ArgumentException("The supplied page token is malformed.", nameof(pageToken));
            }

            cursorCreatedAt = new DateTimeOffset(ticks, TimeSpan.Zero);
        }

        var filtered = _context.Journeys.AsNoTracking()
            .Where(j => statusFilter == null || j.Status == statusFilter);

        var totalCount = await filtered.LongCountAsync(ct);

        // Keyset: rows strictly "after" the cursor in (created_at DESC, journey_id DESC). The
        // row-value comparison (created_at, journey_id) < (c, id) becomes the equivalent OR form.
        var page = filtered
            .Where(j => !hasCursor
                || j.CreatedAt < cursorCreatedAt
                || (j.CreatedAt == cursorCreatedAt && j.JourneyId.CompareTo(cursorJourneyId) < 0))
            .OrderByDescending(j => j.CreatedAt)
            .ThenByDescending(j => j.JourneyId)
            .Take(limit + 1);

        var items = await page.ToListAsync(ct);

        string? nextCursor = null;
        if (items.Count > limit)
        {
            var last = items[limit - 1];
            nextCursor = EncodeCursor(last.CreatedAt, last.JourneyId);
            items.RemoveRange(limit, items.Count - limit);
        }

        return new RepositoryPage<Journey> { Items = items, NextCursor = nextCursor, TotalCount = totalCount };
    }

    /// <inheritdoc />
    public Task<bool> ExistsActiveByNameAsync(
        string name,
        Guid? excludeJourneyId = null,
        CancellationToken ct = default)
    {
        var lowered = name.ToLower();
        return _context.Journeys.AsNoTracking().AnyAsync(
            j => j.Name.ToLower() == lowered
                && j.Status != "Archived"
                && (excludeJourneyId == null || j.JourneyId != excludeJourneyId),
            ct);
    }

    /// <inheritdoc />
    public async Task<DateTimeOffset?> GetUpdatedAtAsync(Guid journeyId, CancellationToken ct = default) =>
        await _context.Journeys.AsNoTracking()
            .Where(j => j.JourneyId == journeyId)
            .Select(j => (DateTimeOffset?)j.UpdatedAt)
            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public async Task CreateAsync(Journey journey, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(journey);
        _context.Journeys.Add(journey);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Journey journey, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(journey);
        _context.Journeys.Update(journey);
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>Encodes a keyset position as an opaque Base64 token (<c>ticks:journeyId</c>).</summary>
    private static string EncodeCursor(DateTimeOffset createdAt, Guid journeyId)
    {
        var raw = $"{createdAt.UtcTicks}:{journeyId:N}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    /// <summary>Decodes an <see cref="EncodeCursor"/> token; false when malformed.</summary>
    private static bool TryDecodeCursor(string token, out long ticks, out Guid journeyId)
    {
        ticks = 0;
        journeyId = Guid.Empty;
        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var separator = raw.IndexOf(':');
            if (separator <= 0)
            {
                return false;
            }

            return long.TryParse(raw.AsSpan(0, separator), out ticks)
                && Guid.TryParseExact(raw.AsSpan(separator + 1), "N", out journeyId);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
