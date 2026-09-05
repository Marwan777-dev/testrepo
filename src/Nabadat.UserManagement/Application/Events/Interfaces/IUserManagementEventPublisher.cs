using Nabadat.UserManagement.Application.Events.Dtos;
using Nabadat.UserManagement.Application.Interfaces;

namespace Nabadat.UserManagement.Application.Events.Interfaces;

/// <summary>
/// Writes an M-17 audit event to the <c>EventLog</c> (EF replacement for the former
/// raw-Npgsql <c>IM17EventPublisher</c>). It persists the row; called inside
/// <c>ITenantDbContext.ExecuteAsync</c> alongside the business change, the wrapping transaction
/// makes the audit row commit or roll back together with that change (FR-015).
/// </summary>
public interface IUserManagementEventPublisher
{
    Task PublishAsync(UserManagementEvent evt, CancellationToken ct = default);
}
