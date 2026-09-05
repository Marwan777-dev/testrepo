namespace Nabadat.CustomerJourneyManagement.Application.Versioning;

/// <summary>
/// Assembles the full journey configuration tree (journey root + scoring/detection config +
/// stages → touchpoints → KPI bindings) from the live tenant schema into a
/// <see cref="JourneySnapshotInput"/> for <see cref="JourneySnapshotSerializer"/> to freeze
/// at publish time (T067 / US-3).
/// <para>
/// It is a seam in front of the raw tenant-schema read so <see cref="JourneyVersionService"/>'s
/// publish orchestration is unit-testable without a database — the unit suite substitutes this
/// port, while the concrete read (which needs touchpoint <c>channels</c>/<c>importance</c> that the
/// M-06 <c>JourneyConfigDto</c> lacks, so it cannot reuse <c>IJourneyConfigReader</c>) is
/// integration-tested, exactly like <c>JourneyConfigReaderService</c>.
/// </para>
/// </summary>
public interface IJourneySnapshotBuilder
{
    /// <summary>
    /// Builds the snapshot input for <paramref name="journeyId"/>, or <c>null</c> when the journey
    /// does not exist (the caller maps that to <c>journey.not_found</c>).
    /// </summary>
    Task<JourneySnapshotInput?> BuildAsync(Guid journeyId, CancellationToken ct = default);
}
