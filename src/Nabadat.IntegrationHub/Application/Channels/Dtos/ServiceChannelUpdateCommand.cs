namespace Nabadat.IntegrationHub.Application.Channels.Dtos;

/// <summary>
/// Input to <c>IServiceChannelService.UpdateAsync</c> — SCR-04's edit submission.
///
/// <para><see cref="ChannelId"/> is <b>nullable on purpose</b>: <c>null</c> means "the client did not
/// submit the field", which a locked channel's read-only form legitimately does (AC-S4-02). The persisted
/// ID then simply stands. A non-null value that differs from the persisted one on a locked channel is a
/// change attempt and is rejected with <c>channel.id_locked</c> (BR-05), even from a stale client that
/// still rendered the field editable.</para>
///
/// <para>Separate from <see cref="ServiceChannelCreateCommand"/> despite the near-identical shape: the two
/// paths differ in what they may change (ID lock, self-exclusion from the uniqueness checks) and merging
/// them would hide that behind an optional id.</para>
/// </summary>
/// <param name="NameEn">Channel name · EN — renaming never touches the ID (BR-06).</param>
/// <param name="NameAr">Channel name · AR.</param>
/// <param name="ChannelId">The new ID, or <c>null</c> to leave it as-is.</param>
/// <param name="Description">Optional free text.</param>
/// <param name="Active">Target status; deactivating hides the channel from new-integration selection (BR-07).</param>
/// <param name="Contract">The full replacement contract — rows absent from this list are removed.</param>
/// <param name="ActorId">The authenticated user performing the edit, for the M-17 audit row.</param>
/// <param name="ActorPersona">That user's persona (<c>P-01</c>…<c>P-08</c>).</param>
public sealed record ServiceChannelUpdateCommand(
    string? NameEn,
    string? NameAr,
    string? ChannelId,
    string? Description,
    bool Active,
    IReadOnlyList<ChannelParameterAssignmentInput>? Contract,
    Guid ActorId,
    string? ActorPersona);
