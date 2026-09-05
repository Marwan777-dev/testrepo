using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.Events;
using Nabadat.CustomerJourneyManagement.Application.Interfaces;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;

namespace Nabadat.CustomerJourneyManagement.Application.Versioning;

/// <summary>
/// Publish/read orchestration for immutable journey versions (T067 / US-3). Implements the two
/// version operations from <c>contracts/journeys-api.md</c>:
/// <list type="bullet">
///   <item><description>
///     <b>Publish</b> — builds the live journey tree (via <see cref="IJourneySnapshotBuilder"/>),
///     freezes it to a self-contained JSON blob (via <see cref="JourneySnapshotSerializer"/>),
///     writes a <c>journey_versions</c> row at <c>max(version_number) + 1</c>, and publishes a
///     <c>journey.version.published</c> M-17 event in the <b>same</b> transaction (FR-015) so the
///     row and the audit event commit atomically. Returns the new version number.
///   </description></item>
///   <item><description>
///     <b>Get</b> — returns the stored snapshot verbatim (the frozen blob captured at publish
///     time, never a freshly recomputed tree), or <c>journey.version_not_found</c>.
///   </description></item>
/// </list>
/// The snapshot is built and serialized <i>before</i> the transaction opens; a missing journey is
/// rejected with <c>journey.not_found</c> and writes nothing.
/// </summary>
public sealed class JourneyVersionService
{
    private readonly IJourneySnapshotBuilder _snapshots;
    private readonly JourneySnapshotSerializer _serializer;
    private readonly IVersionDataService _versions;
    private readonly ITenantDbContext _db;
    private readonly IM17EventPublisher _events;
    private readonly TimeProvider _time;

    public JourneyVersionService(
        IJourneySnapshotBuilder snapshots,
        JourneySnapshotSerializer serializer,
        IVersionDataService versions,
        ITenantDbContext db,
        IM17EventPublisher events,
        TimeProvider time)
    {
        _snapshots = snapshots;
        _serializer = serializer;
        _versions = versions;
        _db = db;
        _events = events;
        _time = time;
    }

    /// <summary>
    /// Publishes the current state of <paramref name="journeyId"/> as the next immutable version on
    /// behalf of <paramref name="actor"/>. Returns the new version number on success (snapshot row +
    /// <c>journey.version.published</c> written in one tx), or a failure carrying
    /// <c>journey.not_found</c> when the journey does not exist. No write occurs on the failure path.
    /// </summary>
    public async Task<ServiceResult<int>> PublishJourneyVersionAsync(
        Guid journeyId,
        ActorContext actor,
        CancellationToken ct = default)
    {
        var input = await _snapshots.BuildAsync(journeyId, ct);
        if (input is null)
        {
            return ServiceResult<int>.Failure("journey.not_found", $"Journey {journeyId} does not exist.");
        }

        // Freeze the tree to a self-contained blob and compute the next sequential number before the
        // transaction opens — the tx body only performs the atomic write + audit.
        var snapshotPayload = _serializer.Serialize(input);
        var nextVersionNumber = await _versions.GetMaxVersionNumberAsync(journeyId, ct) + 1;
        var occurredAt = _time.GetUtcNow();
        var versionId = Guid.NewGuid();

        await _db.ExecuteAsync(async () =>
        {
            var version = new JourneyVersion
            {
                VersionId = versionId,
                JourneyId = journeyId,
                VersionNumber = nextVersionNumber,
                PublishedBy = actor.UserId,
                PublishedAt = occurredAt,
                SnapshotPayload = snapshotPayload,
            };
            await _versions.CreateAsync(version, ct);

            await _events.PublishAsync(
                CustomerJourneyManagementEvent.JourneyVersionPublished(
                    actor.UserId,
                    actor.Persona,
                    versionId,
                    occurredAt,
                    actor.CorrelationId,
                    newValue: new { journeyId, versionNumber = nextVersionNumber }),
                ct);
        }, ct);

        return ServiceResult<int>.Success(nextVersionNumber);
    }

    /// <summary>
    /// Reads version <paramref name="versionNumber"/> of <paramref name="journeyId"/>, returning the
    /// stored snapshot exactly as captured at publish time, or <c>journey.version_not_found</c> when
    /// the version does not exist.
    /// </summary>
    public async Task<ServiceResult<JourneyVersion>> GetJourneyVersionAsync(
        Guid journeyId,
        int versionNumber,
        CancellationToken ct = default)
    {
        var version = await _versions.GetByVersionNumberAsync(journeyId, versionNumber, ct);
        return version is null
            ? ServiceResult<JourneyVersion>.Failure(
                "journey.version_not_found",
                $"Version {versionNumber} of journey {journeyId} does not exist.")
            : ServiceResult<JourneyVersion>.Success(version);
    }

    /// <summary>
    /// Cursor-paginated list of a journey's published versions, newest first (API-04). Thin
    /// pass-through to <see cref="IVersionDataService.ListByJourneyAsync"/> so the API layer stays thin
    /// and consistent with the rest of the module; the page carries the version rows plus the opaque
    /// next cursor and total count. A non-existent journey simply yields an empty page. Read-only.
    /// </summary>
    public Task<RepositoryPage<JourneyVersion>> ListJourneyVersionsAsync(
        Guid journeyId,
        int pageSize,
        string? pageToken,
        CancellationToken ct = default)
        => _versions.ListByJourneyAsync(journeyId, pageSize, pageToken, ct);
}
