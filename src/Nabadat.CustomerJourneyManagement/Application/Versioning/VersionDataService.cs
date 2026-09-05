using System.Text;
using Microsoft.EntityFrameworkCore;
using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.Interfaces;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;

namespace Nabadat.CustomerJourneyManagement.Application.Versioning;

/// <summary>
/// EF <see cref="IVersionDataService"/> over <see cref="ITenantDbContext"/> for the tenant-schema
/// <c>journey_versions</c> table. Replaces the raw-Npgsql <c>VersionRepository</c>: versions are
/// immutable (insert + read only, no UPDATE). The list query is keyset-paginated (API-04)
/// newest-first over <c>version_number</c> — sequential and unique per journey, so a single integer
/// is a stable keyset, Base64-wrapped to stay opaque to clients.
/// </summary>
public sealed class VersionDataService : IVersionDataService
{
    private readonly ITenantDbContext _context;

    public VersionDataService(ITenantDbContext context) => _context = context;

    /// <inheritdoc />
    public Task<JourneyVersion?> GetByVersionNumberAsync(
        Guid journeyId,
        int versionNumber,
        CancellationToken ct = default) =>
        _context.JourneyVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.JourneyId == journeyId && v.VersionNumber == versionNumber, ct);

    /// <inheritdoc />
    public async Task<RepositoryPage<JourneyVersion>> ListByJourneyAsync(
        Guid journeyId,
        int pageSize,
        string? pageToken,
        CancellationToken ct = default)
    {
        var limit = Math.Clamp(pageSize, 1, 200);

        var hasCursor = !string.IsNullOrEmpty(pageToken);
        var cursorVersionNumber = 0;
        if (hasCursor && !TryDecodeCursor(pageToken!, out cursorVersionNumber))
        {
            throw new ArgumentException("The supplied page token is malformed.", nameof(pageToken));
        }

        var forJourney = _context.JourneyVersions.AsNoTracking().Where(v => v.JourneyId == journeyId);

        var totalCount = await forJourney.LongCountAsync(ct);

        var page = forJourney
            .Where(v => !hasCursor || v.VersionNumber < cursorVersionNumber)
            .OrderByDescending(v => v.VersionNumber)
            .Take(limit + 1);

        var items = await page.ToListAsync(ct);

        string? nextCursor = null;
        if (items.Count > limit)
        {
            nextCursor = EncodeCursor(items[limit - 1].VersionNumber);
            items.RemoveRange(limit, items.Count - limit);
        }

        return new RepositoryPage<JourneyVersion> { Items = items, NextCursor = nextCursor, TotalCount = totalCount };
    }

    /// <inheritdoc />
    public async Task<int> GetMaxVersionNumberAsync(Guid journeyId, CancellationToken ct = default)
    {
        // No versions yet → MaxAsync over an empty set would throw, so project to int? and coalesce.
        var max = await _context.JourneyVersions.AsNoTracking()
            .Where(v => v.JourneyId == journeyId)
            .Select(v => (int?)v.VersionNumber)
            .MaxAsync(ct);
        return max ?? 0;
    }

    /// <inheritdoc />
    public async Task CreateAsync(JourneyVersion version, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(version);
        _context.JourneyVersions.Add(version);
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>Encodes a keyset position (the last-seen version number) as an opaque Base64 token.</summary>
    private static string EncodeCursor(int versionNumber)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(versionNumber.ToString()));

    /// <summary>Decodes an <see cref="EncodeCursor"/> token; false when malformed.</summary>
    private static bool TryDecodeCursor(string token, out int versionNumber)
    {
        versionNumber = 0;
        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            return int.TryParse(raw, out versionNumber);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
