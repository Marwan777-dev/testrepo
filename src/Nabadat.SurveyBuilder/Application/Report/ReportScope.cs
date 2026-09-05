namespace Nabadat.SurveyBuilder.Application.Report;

/// <summary>
/// The caller's data scope applied to a report query server-side, before the ES query is dispatched
/// (APIs-constitution Article 4.5). <see cref="Assignments"/> mirrors the session's
/// <c>PermissionSnapshot.ScopeAssignments</c> — a parameter name (e.g. <c>branch</c>, <c>region</c>)
/// mapped to the values the caller is allowed to see. An empty map is organisation-wide (P-01/P-02):
/// no additional filter clause is added.
/// </summary>
public sealed record ReportScope(IReadOnlyDictionary<string, IReadOnlyList<string>> Assignments)
{
    /// <summary>An unrestricted, organisation-wide scope (no scope filter clauses).</summary>
    public static readonly ReportScope Organisation =
        new(new Dictionary<string, IReadOnlyList<string>>());

    /// <summary><c>true</c> when no scope parameter narrows the result set.</summary>
    public bool IsOrganisationWide => Assignments.Count == 0;
}
