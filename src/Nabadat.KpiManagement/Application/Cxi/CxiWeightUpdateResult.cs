namespace Nabadat.KpiManagement.Application.Cxi;

/// <summary>
/// Outcome of <see cref="CxiWeightUpdateService.ReplaceAsync"/>. On failure, <see cref="ErrorCode"/>
/// is one of the <see cref="CxiWeightUpdateService"/> contract codes and no rows/events were written.
/// </summary>
public sealed record CxiWeightUpdateResult(bool Succeeded, string? ErrorCode)
{
    public static CxiWeightUpdateResult Ok() => new(true, null);

    public static CxiWeightUpdateResult Fail(string code) => new(false, code);
}
