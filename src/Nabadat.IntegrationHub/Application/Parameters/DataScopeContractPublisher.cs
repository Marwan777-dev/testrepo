using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nabadat.IntegrationHub.Application.Interfaces;
using Nabadat.IntegrationHub.Application.Parameters.Dtos;
using Nabadat.IntegrationHub.Application.Parameters.Interfaces;

namespace Nabadat.IntegrationHub.Application.Parameters;

/// <summary>
/// T059 — implements BR-10's forward half: "M-10 data-scope filters are built on M-13 parameter definitions and
/// value sets." Whenever a parameter or one of its mappings changes, the qualifying parameters' name, EN label,
/// and known value set are pushed to M-10's <b>real, already-built</b>
/// <c>POST /api/v1/authorization/scope/parameters</c> endpoint (research.md §4.1) — this is a live cross-module
/// integration, not a stub.
///
/// <para><b>Which parameters qualify.</b> M-10 rejects any definition with an empty <c>allowedValues</c> set, so
/// a parameter is only pushed when it is <i>enabled</i>, <i>filterable or mapping-enabled</i>, and actually has an
/// enumerable value set. In practice that means the mapping table: a List parameter's distinct source values are
/// its value set, while a free-text or numeric parameter has none and is therefore excluded — exactly the
/// reconciliation research.md §4.1 flagged for implementation time.</para>
///
/// <para><b>Reserved names.</b> M-10 refuses <c>user_id</c>, <c>tenant_id</c>, <c>persona</c>, <c>id</c>,
/// <c>node_id</c>, <c>assignment_id</c>, <c>rule_id</c> — they would shadow real columns when a scope filter is
/// applied. None of M-13's 23 built-ins collide (checked against the seed), but a tenant is free to create a
/// custom parameter called <c>persona</c>, so the list is filtered here rather than assumed away. Dropping the
/// definition locally is better than shipping a batch M-10 rejects <i>wholesale</i> — its validator fails the
/// entire payload on one bad name, which would silently strand every other parameter's value set.</para>
///
/// <para><b>Failure is not fatal.</b> The push is a projection of data M-13 already owns; if M-10 is unreachable
/// the tenant's own catalogue is still correct and the next parameter change re-pushes the full set. So the
/// publisher logs and swallows rather than propagating — a down M-10 must not make the console's Create button
/// fail. Callers invoke it <b>after</b> the transaction commits for the same reason.</para>
/// </summary>
public sealed class DataScopeContractPublisher
{
    /// <summary>The <c>source_module</c> M-10 records against every definition M-13 provides.</summary>
    public const string SourceModule = "M-13";

    /// <summary>M-10's per-payload ceiling (<c>M13ParameterContractAdapter.MaxDefinitions</c>).</summary>
    public const int MaxDefinitionsPerPayload = 500;

    /// <summary>
    /// Mirrors <c>M13ParameterContractAdapter.ReservedNames</c>. Duplicated deliberately — it is part of M-10's
    /// published HTTP contract, and coupling M-13's compilation to M-10's private static field would be worse
    /// than a documented mirror. A drift here costs one rejected batch, which the integration test catches.
    /// </summary>
    private static readonly IReadOnlySet<string> ReservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "user_id", "tenant_id", "persona", "id", "node_id", "assignment_id", "rule_id",
    };

    private readonly ITenantDbContext _db;
    private readonly IDataScopeContractClient _client;
    private readonly ILogger<DataScopeContractPublisher> _logger;

    public DataScopeContractPublisher(
        ITenantDbContext db,
        IDataScopeContractClient client,
        ILogger<DataScopeContractPublisher> logger)
    {
        _db = db;
        _client = client;
        _logger = logger;
    }

    /// <summary>
    /// Rebuilds and pushes the tenant's full qualifying set. A full push rather than a delta because M-10's
    /// endpoint <i>upserts by name</i> and holds no notion of a partial update — sending everything is both
    /// simpler and self-healing after a failed push.
    /// </summary>
    /// <returns>The number of definitions successfully pushed; <c>0</c> when nothing qualified or the push failed.</returns>
    public async Task<int> PublishAsync(CancellationToken ct = default)
    {
        var definitions = await BuildAsync(ct);

        if (definitions.Count == 0)
        {
            return 0;
        }

        var published = 0;

        foreach (var batch in definitions.Chunk(MaxDefinitionsPerPayload))
        {
            try
            {
                await _client.PublishAsync(new DataScopeContractPayload(SourceModule, batch), ct);
                published += batch.Length;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // See the class remarks: the tenant's own catalogue is already committed and correct.
                _logger.LogWarning(
                    ex,
                    "Pushing {Count} data-scope parameter definitions to M-10 failed; M-13's catalogue is unaffected and the next parameter change will re-push.",
                    batch.Length);
                return published;
            }
        }

        return published;
    }

    /// <summary>
    /// Projects the qualifying parameters into M-10's wire shape. Exposed for the integration lane, which asserts
    /// the selection rules without needing a live M-10.
    /// </summary>
    public async Task<IReadOnlyList<DataScopeParameterContract>> BuildAsync(CancellationToken ct = default)
    {
        var candidates = await _db.Parameters
            .AsNoTracking()
            .Where(p => p.Enabled && (p.Filterable || p.MappingSupport))
            .OrderBy(p => p.ApiField)
            .Select(p => new { p.Id, p.ApiField, p.NameEn })
            .ToListAsync(ct);

        if (candidates.Count == 0)
        {
            return Array.Empty<DataScopeParameterContract>();
        }

        var ids = candidates.Select(c => c.Id).ToList();

        // The mapping table is the sole source of a parameter's known value set (BR-12): List membership is never
        // enforced at ingestion, so what has been mapped IS what M-10 may offer as filter options.
        var values = await _db.ParameterMappings
            .AsNoTracking()
            .Where(m => ids.Contains(m.ParameterId))
            .OrderBy(m => m.SourceValue)
            .Select(m => new { m.ParameterId, m.SourceValue })
            .ToListAsync(ct);

        var byParameter = values
            .GroupBy(v => v.ParameterId)
            .ToDictionary(g => g.Key, g => g.Select(v => v.SourceValue).Distinct(StringComparer.Ordinal).ToList());

        var definitions = new List<DataScopeParameterContract>(candidates.Count);

        foreach (var candidate in candidates)
        {
            if (ReservedNames.Contains(candidate.ApiField))
            {
                continue;
            }

            // No mapped values means no enumerable value set — M-10 would reject the whole batch for it.
            if (!byParameter.TryGetValue(candidate.Id, out var allowed) || allowed.Count == 0)
            {
                continue;
            }

            definitions.Add(new DataScopeParameterContract(candidate.ApiField, candidate.NameEn, allowed));
        }

        return definitions;
    }
}
