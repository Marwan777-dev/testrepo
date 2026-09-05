using Microsoft.EntityFrameworkCore;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.Interfaces;
using Nabadat.UserManagement.Application.Permissions.Interfaces;
using Nabadat.UserManagement.Application.Interfaces;

namespace Nabadat.UserManagement.Application.Permissions;

/// <summary>EF <see cref="IDataScopeService"/> over <see cref="ITenantDbContext"/>.</summary>
public sealed class DataScopeService : IDataScopeService
{
    private readonly ITenantDbContext _context;

    public DataScopeService(ITenantDbContext context) => _context = context;

    public async Task<IReadOnlyList<DataScopeAssignment>> GetScopeAssignmentsAsync(Guid userId, CancellationToken ct = default) =>
        await _context.DataScopeAssignments
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.ParameterName)
            .ToListAsync(ct);

    public async Task ReplaceScopeAssignmentsAsync(
        Guid userId,
        IReadOnlyList<DataScopeAssignment> assignments,
        CancellationToken ct = default)
    {
        await _context.DataScopeAssignments
            .Where(a => a.UserId == userId)
            .ExecuteDeleteAsync(ct);

        foreach (var assignment in assignments)
        {
            _context.DataScopeAssignments.Add(new DataScopeAssignment
            {
                AssignmentId = assignment.AssignmentId == Guid.Empty ? Guid.NewGuid() : assignment.AssignmentId,
                UserId = userId,
                ParameterName = assignment.ParameterName,
                AllowedValues = assignment.AllowedValues,
                CreatedAt = assignment.CreatedAt,
                UpdatedAt = assignment.UpdatedAt,
            });
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<DataScopeParameterDefinition>> GetParameterDefinitionsAsync(CancellationToken ct = default) =>
        await _context.DataScopeParameterDefinitions
            .AsNoTracking()
            .OrderBy(d => d.ParameterName)
            .ToListAsync(ct);

    public async Task StoreParameterDefinitionsAsync(
        IReadOnlyList<DataScopeParameterDefinition> definitions,
        CancellationToken ct = default)
    {
        foreach (var definition in definitions)
        {
            var existing = await _context.DataScopeParameterDefinitions
                .FirstOrDefaultAsync(d => d.ParameterName == definition.ParameterName, ct);

            if (existing is null)
            {
                _context.DataScopeParameterDefinitions.Add(definition);
            }
            else
            {
                existing.Label = definition.Label;
                existing.AllowedValues = definition.AllowedValues;
                existing.SourceModule = definition.SourceModule;
                existing.UpdatedAt = definition.UpdatedAt;
            }
        }

        await _context.SaveChangesAsync(ct);
    }
}
