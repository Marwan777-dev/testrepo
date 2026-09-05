using Nabadat.SurveyBuilder.Domain.Interfaces;

namespace Nabadat.SurveyBuilder.Infrastructure.CrossModule;

/// <summary>
/// Placeholder <see cref="ITenantDesignGuidelinesReader"/> until M-11 ships (T020, TODO-M01-006).
/// Returns the default Nabadat palette so Inherited-mode appearance resolves for dev/E2E. Swap for
/// the real M-11 adapter in the host when M-11 lands.
/// </summary>
public sealed class DevTenantDesignGuidelinesReader : ITenantDesignGuidelinesReader
{
    private static readonly TenantDesignGuidelines Default = new(
        PrimaryColour: "#0D8BBC",
        TextColour: "#1E2235",
        ButtonRadiusPx: 12);

    public Task<TenantDesignGuidelines> GetDesignGuidelinesAsync(CancellationToken ct = default) =>
        Task.FromResult(Default);
}
