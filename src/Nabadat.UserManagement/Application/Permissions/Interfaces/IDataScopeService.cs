using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Application.Interfaces;

namespace Nabadat.UserManagement.Application.Permissions.Interfaces;

/// <summary>
/// Context-holding data-access service (tenant schema, EF / <c>TenantDbContext</c>) for
/// parameter-based data scope — a user's allowed-value assignments
/// (<c>data_scope_assignments</c>) and the M-13 parameter definitions
/// (<c>data_scope_parameter_definitions</c>). Replaces the raw-Npgsql
/// <c>IDataScopeRepository</c>. Write methods persist immediately; compose them inside
/// <c>ITenantDbContext.ExecuteAsync</c> to commit atomically with other writes.
/// </summary>
public interface IDataScopeService
{
    Task<IReadOnlyList<DataScopeAssignment>> GetScopeAssignmentsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Replaces a user's scope assignments (delete-then-insert) and saves.</summary>
    Task ReplaceScopeAssignmentsAsync(
        Guid userId,
        IReadOnlyList<DataScopeAssignment> assignments,
        CancellationToken ct = default);

    Task<IReadOnlyList<DataScopeParameterDefinition>> GetParameterDefinitionsAsync(CancellationToken ct = default);

    /// <summary>Upserts the supplied parameter definitions by name and saves.</summary>
    Task StoreParameterDefinitionsAsync(
        IReadOnlyList<DataScopeParameterDefinition> definitions,
        CancellationToken ct = default);
}
