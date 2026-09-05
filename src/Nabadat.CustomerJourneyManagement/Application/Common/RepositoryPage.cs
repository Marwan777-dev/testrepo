namespace Nabadat.CustomerJourneyManagement.Application.Common;

/// <summary>
/// A cursor-paginated page of data-access results (API-04 cursor pagination). Returned by
/// list queries that page over large result sets (journeys, journey versions).
/// </summary>
public sealed record RepositoryPage<T>
{
    /// <summary>The rows in this page, in query order.</summary>
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>Opaque cursor for the next page; null when there are no more results.</summary>
    public string? NextCursor { get; init; }

    /// <summary>Total number of rows matching the query across all pages.</summary>
    public long TotalCount { get; init; }
}
