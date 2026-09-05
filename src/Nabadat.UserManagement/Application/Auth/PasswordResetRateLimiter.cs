using Microsoft.EntityFrameworkCore;
using Nabadat.UserManagement.Application.Auth.Exceptions;
using Nabadat.UserManagement.Application.Auth.Interfaces;
using Nabadat.UserManagement.Application.Interfaces;
using Nabadat.UserManagement.Domain.Entities;
using System.Security.Cryptography;
using System.Text;

namespace Nabadat.UserManagement.Application.Auth;

/// <summary>
/// Database-backed sliding-window limiter: at most 3 self-service reset requests per
/// email per 30-minute window. The 4th request in a window is rejected and audited
/// (<c>password.reset.rate_limited</c>); the window resets once it elapses.
///
/// <para>Uses the <see cref="TenantDbContext"/> directly as the unit of work (DB-08):
/// the window record and — on rejection — the audit <see cref="EventLog"/> are tracked
/// in the context and persisted by a single <c>SaveChangesAsync</c>. No repository, no
/// <c>IUnitOfWork</c>, no raw SQL.</para>
/// </summary>
public sealed class PasswordResetRateLimiter : IPasswordResetRateLimiter
{
    private const int MaxRequests = 3;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(30);

    private readonly ITenantDbContext _context;
    private readonly TimeProvider _clock;

    public PasswordResetRateLimiter(ITenantDbContext db, TimeProvider clock)
    {
        _context = db;
        _clock = clock;
    }

    public async Task EnsureWithinLimitAsync(string email, CancellationToken ct = default)
    {
        var emailHash = HashEmail(email);
        var now = _clock.GetUtcNow();
        var record = await _context.PasswordResetRateLimits
            .FirstOrDefaultAsync(r => r.EmailHash == emailHash, ct);

        // New window: no record yet, or the existing window has elapsed.
        if (record is null || record.WindowStartUtc + Window <= now)
        {
            if (record is null)
            {
                _context.PasswordResetRateLimits.Add(new PasswordResetRateLimitRecord
                {
                    EmailHash = emailHash,
                    WindowStartUtc = now,
                    RequestCount = 1,
                    UpdatedAt = now,
                });
            }
            else
            {
                record.WindowStartUtc = now;
                record.RequestCount = 1;
                record.UpdatedAt = now;
            }

            await _context.SaveChangesAsync(ct);
            return;
        }

        if (record.RequestCount < MaxRequests)
        {
            record.RequestCount = (short)(record.RequestCount + 1);
            record.UpdatedAt = now;
            await _context.SaveChangesAsync(ct);
            return;
        }

        // 4th+ request inside the window: append the audit event and reject. The
        // EventLog row is tracked in THIS context and committed by one SaveChangesAsync,
        // so the audit write is atomic (no business state changes on this path) — DB-08.
        _context.EventLogs.Add(new EventLog
        {
            EventId = Guid.NewGuid(),
            EventType = "password.reset.rate_limited",
            ActorId = Guid.Empty,
            ActorPersona = string.Empty,
            EntityType = "PasswordReset",
            EntityId = Guid.Empty,
            OccurredAtUtc = now,
            CorrelationId = Guid.NewGuid(),
        });
        await _context.SaveChangesAsync(ct);

        var retryAfter = (int)(record.WindowStartUtc + Window - now).TotalSeconds;
        throw new PasswordResetRateLimitExceededException(Math.Max(retryAfter, 0));
    }

    private static byte[] HashEmail(string email) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));
}
