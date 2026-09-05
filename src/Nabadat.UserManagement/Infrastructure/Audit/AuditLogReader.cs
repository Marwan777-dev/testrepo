using Microsoft.EntityFrameworkCore;
using Nabadat.UserManagement.Application.Interfaces;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.Interfaces;
using Nabadat.UserManagement.Domain.ValueObjects;

namespace Nabadat.UserManagement.Infrastructure.Audit;

/// <summary>
/// M-10's own read-only view over the tenant-schema <c>event_log</c>
/// (<see cref="IAuditLogReader"/>): a filtered, newest-first, keyset-paginated query over
/// <see cref="ITenantDbContext.EventLogs"/> — the same table M-10 appends its audit events
/// to, so the module owns the full audit cycle for its own events (no external M-17
/// dependency; resolves gap-analysis I-02/I-04). Strictly read-only — never mutates
/// <c>event_log</c>.
/// </summary>
public sealed class AuditLogReader : IAuditLogReader
{
    private readonly ITenantDbContext _db;

    public AuditLogReader(ITenantDbContext db) => _db = db;

    public async Task<AuditLogPage> QueryEventsAsync(
        AuditLogFilter filter,
        int pageSize,
        string? cursor,
        CancellationToken ct = default)
    {
        var query = _db.EventLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.EventType))
        {
            query = query.Where(e => e.EventType == filter.EventType);
        }

        if (filter.FromUtc is { } from)
        {
            query = query.Where(e => e.OccurredAtUtc >= from);
        }

        if (filter.ToUtc is { } to)
        {
            query = query.Where(e => e.OccurredAtUtc <= to);
        }

        if (filter.ActorId is { } actorId)
        {
            query = query.Where(e => e.ActorId == actorId);
        }

        if (filter.EntityId is { } entityId)
        {
            query = query.Where(e => e.EntityId == entityId);
        }

        // Keyset pagination on the newest-first timestamp. event_log rows are written one
        // per committed transaction with microsecond-precision timestamps, so a same-instant
        // tie straddling a page boundary is not a practical concern at this audit volume.
        if (TryDecodeCursor(cursor, out var after))
        {
            query = query.Where(e => e.OccurredAtUtc < after);
        }

        // Fetch one extra row to detect whether a further page exists.
        var rows = await query
            .OrderByDescending(e => e.OccurredAtUtc)
            .ThenByDescending(e => e.EventId)
            .Take(pageSize + 1)
            .ToListAsync(ct);

        var hasMore = rows.Count > pageSize;
        var page = rows.Take(pageSize).ToList();

        return new AuditLogPage
        {
            Items = page.Select(Map).ToList(),
            NextCursor = hasMore && page.Count > 0 ? EncodeCursor(page[^1].OccurredAtUtc) : null,
        };
    }

    private static AuditLogEntry Map(EventLog e) => new()
    {
        EventId = e.EventId,
        EventType = e.EventType,
        ActorId = e.ActorId,
        ActorPersona = e.ActorPersona,
        EntityType = e.EntityType,
        EntityId = e.EntityId,
        OldValueJson = e.OldValue,
        NewValueJson = e.NewValue,
        OccurredAtUtc = e.OccurredAtUtc,
        CorrelationId = e.CorrelationId,
    };

    /// <summary>Opaque cursor = base64 of the last row's UTC tick count.</summary>
    private static string EncodeCursor(DateTimeOffset occurredAt) =>
        Convert.ToBase64String(BitConverter.GetBytes(occurredAt.UtcTicks));

    private static bool TryDecodeCursor(string? cursor, out DateTimeOffset after)
    {
        after = default;
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return false;
        }

        try
        {
            var bytes = Convert.FromBase64String(cursor);
            if (bytes.Length != sizeof(long))
            {
                return false;
            }

            after = new DateTimeOffset(BitConverter.ToInt64(bytes), TimeSpan.Zero);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
