using Microsoft.EntityFrameworkCore;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.Interfaces;
using Nabadat.UserManagement.Application.Permissions.Interfaces;
using Nabadat.UserManagement.Application.Interfaces;

namespace Nabadat.UserManagement.Application.Permissions;

/// <summary>
/// EF <see cref="IPermissionModuleAssignmentService"/> over <see cref="ITenantDbContext"/>.
/// </summary>
public sealed class PermissionModuleAssignmentService : IPermissionModuleAssignmentService
{
    private readonly ITenantDbContext _context;

    public PermissionModuleAssignmentService(ITenantDbContext context) => _context = context;

    public async Task<IReadOnlyList<PermissionModuleAssignment>> GetAssignmentsAsync(Guid userId, CancellationToken ct = default) =>
        await _context.PermissionModuleAssignments
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.ModuleId)
            .ToListAsync(ct);

    public async Task ReplaceAssignmentsAsync(
        Guid userId,
        IReadOnlyList<PermissionModuleAssignment> assignments,
        CancellationToken ct = default)
    {
        // Delete-then-insert: ExecuteDelete runs immediately on the ambient transaction so
        // it precedes the staged inserts (mirrors the prior DELETE … INSERT).
        await _context.PermissionModuleAssignments
            .Where(a => a.UserId == userId)
            .ExecuteDeleteAsync(ct);

        foreach (var assignment in assignments)
        {
            _context.PermissionModuleAssignments.Add(new PermissionModuleAssignment
            {
                AssignmentId = assignment.AssignmentId == Guid.Empty ? Guid.NewGuid() : assignment.AssignmentId,
                UserId = userId,
                ModuleId = assignment.ModuleId,
                AllowedModes = assignment.AllowedModes,
                AssignedBy = assignment.AssignedBy,
                CreatedAt = assignment.CreatedAt,
                UpdatedAt = assignment.UpdatedAt,
            });
        }

        await _context.SaveChangesAsync(ct);
    }
}
