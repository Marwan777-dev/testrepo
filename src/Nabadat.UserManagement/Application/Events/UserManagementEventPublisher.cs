using Nabadat.UserManagement.Application.Events;
using Nabadat.UserManagement.Application.Events.Dtos;
using Nabadat.UserManagement.Application.Events.Interfaces;
using Nabadat.UserManagement.Application.Interfaces;

namespace Nabadat.UserManagement.Application.Events;

/// <summary>
/// EF <see cref="IUserManagementEventPublisher"/>: maps the event to an <c>EventLog</c> (via
/// <see cref="EventLogFactory"/>), adds it to the scoped <see cref="ITenantDbContext"/>, and
/// saves. Called inside an <c>ITenantDbContext.ExecuteAsync</c>, the wrapping transaction makes this
/// audit row commit or roll back together with the business change (FR-015).
/// </summary>
public sealed class UserManagementEventPublisher : IUserManagementEventPublisher
{
    private readonly ITenantDbContext _context;

    public UserManagementEventPublisher(ITenantDbContext context) => _context = context;

    public async Task PublishAsync(UserManagementEvent evt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evt);
        _context.EventLogs.Add(evt.ToEventLog());
        await _context.SaveChangesAsync(ct);
    }


}
