namespace Nabadat.UserManagement.Api.Contracts;

/// <summary>Response for <c>GET /api/v1/users</c> — one cursor-paginated page (API-04).</summary>
public sealed record UserListResponse
{
    public required IReadOnlyList<UserSummaryResponse> Items { get; init; }

    public string? NextPageToken { get; init; }

    public required int TotalCount { get; init; }
}
