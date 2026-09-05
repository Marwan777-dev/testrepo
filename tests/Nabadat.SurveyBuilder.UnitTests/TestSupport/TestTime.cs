using Microsoft.Extensions.Time.Testing;

namespace Nabadat.SurveyBuilder.UnitTests.TestSupport;

/// <summary>
/// Shared deterministic time anchor for M-01 unit tests (CLAUDE.md Unit Test Policy rule 8/14).
/// Production code takes a <see cref="TimeProvider"/>; tests inject <see cref="Provider"/> so
/// timestamps are fixed and assertions are stable.
/// </summary>
public static class TestTime
{
    /// <summary>Fixed UTC instant every test clock starts at.</summary>
    public static readonly DateTimeOffset Anchor = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>A fresh <see cref="FakeTimeProvider"/> pinned to <see cref="Anchor"/>.</summary>
    public static FakeTimeProvider Provider() => new(Anchor);
}
