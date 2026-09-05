namespace Nabadat.IntegrationHub.Application.Channels.Dtos;

/// <summary>
/// Input to <c>IServiceChannelService.CreateAsync</c> — SCR-04's create submission (FR-S4-01…04).
///
/// <para>The actor is carried on the command rather than read from an ambient accessor: the Api layer owns
/// session resolution (<c>ISessionContextAccessor</c>) and passes the actor down, which keeps the
/// Application layer free of any HTTP/session dependency and makes the audit row's attribution explicit.</para>
/// </summary>
/// <param name="NameEn">Channel name · EN — required, ≤ 50 chars, unique per tenant (VR-F02).</param>
/// <param name="NameAr">Channel name · AR — required (VR-F03).</param>
/// <param name="ChannelId">The inbound path segment — sanitised server-side to <c>[A-Za-z0-9-]</c>, ≤ 19 chars (VR-F04).</param>
/// <param name="Description">Optional free text.</param>
/// <param name="Active">Initial status; inactive channels reject requests with <c>E-1004</c> (BR-07).</param>
/// <param name="Contract">The parameter-contract rows (may be empty — a channel can start supporting nothing).</param>
/// <param name="ActorId">The authenticated user creating the channel, for the M-17 audit row.</param>
/// <param name="ActorPersona">That user's persona (<c>P-01</c>…<c>P-08</c>).</param>
public sealed record ServiceChannelCreateCommand(
    string? NameEn,
    string? NameAr,
    string? ChannelId,
    string? Description,
    bool Active,
    IReadOnlyList<ChannelParameterAssignmentInput>? Contract,
    Guid ActorId,
    string? ActorPersona);
