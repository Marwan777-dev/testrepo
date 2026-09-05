using Nabadat.UserManagement.Domain.Entities;

namespace Nabadat.UserManagement.Domain.ValueObjects;

/// <summary>
/// One page of a tenant-user listing (API-04 cursor pagination). <see cref="NextPageToken"/>
/// is the opaque cursor for the following page, or <c>null</c> when the last page is reached.
/// </summary>
public sealed record UserListPage
{
    public required IReadOnlyList<TenantUser> Items { get; init; }

    public string? NextPageToken { get; init; }

    public required int TotalCount { get; init; }
}
