using Nabadat.UserManagement.Application.Permissions.Exceptions;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.Interfaces;
using Nabadat.UserManagement.Application.Permissions.Interfaces;
using Nabadat.UserManagement.Application.Interfaces;

namespace Nabadat.UserManagement.Application.Permissions;

/// <summary>
/// Ingests external scope parameter definitions (T104, US3) for
/// <c>POST /api/v1/authorization/scope/parameters</c>. Provider-agnostic: it stores
/// whatever names and values the payload carries with no hardcoded provider branching.
/// A payload is rejected (with a <see cref="ValidationException"/>, before any write)
/// when it carries a reserved system name, a definition with no allowed values, or
/// more than <see cref="MaxDefinitions"/> definitions (permissions-api.md). Valid
/// definitions are upserted into <c>data_scope_parameter_definitions</c>.
/// </summary>
public sealed class M13ParameterContractAdapter
{
    /// <summary>Per-payload ceiling (permissions-api.md rate limit).</summary>
    private const int MaxDefinitions = 500;

    /// <summary>
    /// Names that collide with system identity/structural fields and must never become
    /// scope parameters (they would shadow real columns when scope filters are applied).
    /// </summary>
    private static readonly IReadOnlySet<string> ReservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "user_id", "tenant_id", "persona", "id", "node_id", "assignment_id", "rule_id",
    };

    private readonly IDataScopeService _scopes;
    private readonly ITenantDbContext _context;
    private readonly TimeProvider _clock;

    public M13ParameterContractAdapter(IDataScopeService scopes, ITenantDbContext context, TimeProvider clock)
    {
        _scopes = scopes;
        _context = context;
        _clock = clock;
    }

    public async Task StoreParameterDefinitionsAsync(M13ParameterPayload payload, CancellationToken ct = default)
    {
        var failures = new List<ValidationFailure>();

        if (payload.Parameters.Count > MaxDefinitions)
        {
            failures.Add(new ValidationFailure("parameters", "limit_exceeded"));
        }

        for (var i = 0; i < payload.Parameters.Count; i++)
        {
            var parameter = payload.Parameters[i];
            if (string.IsNullOrWhiteSpace(parameter.Name) || ReservedNames.Contains(parameter.Name))
            {
                failures.Add(new ValidationFailure($"parameters[{i}].name", "reserved"));
            }

            if (parameter.AllowedValues.Count == 0)
            {
                failures.Add(new ValidationFailure($"parameters[{i}].allowedValues", "empty"));
            }
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        var now = _clock.GetUtcNow();
        var definitions = payload.Parameters.Select(parameter => new DataScopeParameterDefinition
        {
            ParameterName = parameter.Name,
            Label = parameter.Label,
            AllowedValues = parameter.AllowedValues,
            SourceModule = payload.SourceModule,
            CreatedAt = now,
            UpdatedAt = now,
        }).ToList();

        await _context.ExecuteAsync(() => _scopes.StoreParameterDefinitionsAsync(definitions, ct), ct);
    }
}
