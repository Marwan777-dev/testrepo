namespace Nabadat.IntegrationHub.Application.Channels;

/// <summary>
/// One channel-contract row's two flags after <see cref="ParameterContractDependencyRule"/> has normalised
/// them: <paramref name="Required"/> can only be <c>true</c> while <paramref name="Supported"/> is
/// <c>true</c> (FR-S4-04), which is also the baseline's
/// <c>ck_channel_parameter_assignments_required_needs_supported</c> CHECK.
/// </summary>
/// <param name="Supported">Whether the channel's backend may send this parameter.</param>
/// <param name="Required">Whether it is mandatory — authoritative at request time over the parameter-level default (BR-08).</param>
public sealed record ContractFlags(bool Supported, bool Required);
