namespace Nabadat.IntegrationHub.Application.Channels.Dtos;

/// <summary>
/// One persisted channel-contract row, projected for SCR-04's contract table. Carries the parameter's
/// identity fields alongside the two flags so the form can render the table without a second round-trip to
/// the parameter catalogue.
/// </summary>
/// <param name="ParameterId">The catalogue parameter.</param>
/// <param name="ApiField">Its <c>snake_case</c> wire key (the caller-facing name).</param>
/// <param name="NameEn">Its English display name.</param>
/// <param name="NameAr">Its Arabic display name.</param>
/// <param name="Supported">Whether the channel's backend may send it.</param>
/// <param name="Required">Whether it is mandatory at request time (BR-08).</param>
public sealed record ChannelContractRowDto(
    Guid ParameterId,
    string ApiField,
    string NameEn,
    string NameAr,
    bool Supported,
    bool Required);
