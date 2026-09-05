namespace Nabadat.IntegrationHub.Application.Channels.Dtos;

/// <summary>
/// One submitted row of SCR-04's parameter-contract table (FR-S4-04). The client sends a row per
/// parameter it toggled; <see cref="ParameterContractDependencyRule"/> normalises the pair before it is
/// persisted, and rows where both flags are <c>false</c> carry no signal and are not stored.
/// </summary>
/// <param name="ParameterId">The catalogue parameter this row configures (built-in or custom).</param>
/// <param name="Supported">Whether the channel's backend may send it.</param>
/// <param name="Required">Whether it is mandatory — only honoured while <paramref name="Supported"/> (FR-S4-04).</param>
public sealed record ChannelParameterAssignmentInput(Guid ParameterId, bool Supported, bool Required);
