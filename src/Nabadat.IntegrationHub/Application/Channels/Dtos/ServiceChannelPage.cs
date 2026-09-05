namespace Nabadat.IntegrationHub.Application.Channels.Dtos;

/// <summary>
/// One cursor page of service channels (API-04: cursor pagination, never offset). <see cref="NextCursor"/>
/// is opaque to the client and <c>null</c> once the list is exhausted.
/// </summary>
/// <param name="Items">The page's channels, ordered by EN name (case-insensitively), then id.</param>
/// <param name="NextCursor">Opaque continuation token, or <c>null</c> at the end of the list.</param>
public sealed record ServiceChannelPage(IReadOnlyList<ServiceChannelDto> Items, string? NextCursor);
