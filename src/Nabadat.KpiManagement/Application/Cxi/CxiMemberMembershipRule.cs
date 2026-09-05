namespace Nabadat.KpiManagement.Application.Cxi;

/// <summary>
/// T085 [US-3] — the membership-set transitions for a CXI composite's members: a member is
/// auto-removed when its KPI is deactivated elsewhere (FR-026 / FR-044), and the CXI may never
/// include itself (data-model.md §1.4). Constructed with the CXI KPI's own id so it can reject
/// self-membership.
/// </summary>
public sealed class CxiMemberMembershipRule
{
    private readonly Guid _cxiKpiId;

    public CxiMemberMembershipRule(Guid cxiKpiId) => _cxiKpiId = cxiKpiId;

    /// <summary>
    /// Returns the member set with <paramref name="deactivatedKpiId"/> removed, preserving order
    /// (the cascade run when a member KPI is deactivated).
    /// </summary>
    public IReadOnlyList<Guid> OnKpiDeactivated(IReadOnlyList<Guid> memberSet, Guid deactivatedKpiId)
    {
        ArgumentNullException.ThrowIfNull(memberSet);
        return memberSet.Where(id => id != deactivatedKpiId).ToList();
    }

    /// <summary>
    /// Returns the member set with <paramref name="candidate"/> appended (idempotent if already
    /// present). Throws <see cref="CxiCannotIncludeItself"/> when the candidate is the CXI itself.
    /// </summary>
    public IReadOnlyList<Guid> Add(IReadOnlyList<Guid> memberSet, Guid candidate)
    {
        ArgumentNullException.ThrowIfNull(memberSet);

        if (candidate == _cxiKpiId)
        {
            throw new CxiCannotIncludeItself(_cxiKpiId);
        }

        return memberSet.Contains(candidate) ? memberSet : [.. memberSet, candidate];
    }
}
