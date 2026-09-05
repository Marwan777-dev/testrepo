using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Report;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Report;

/// <summary>
/// T231 [US8] — unit tests for <c>VerbatimSampler</c> (FR-13.7). Text/paragraph questions surface a
/// sample of recent verbatim responses in the report; the "show more" control reveals up to the last
/// 100 received. The sampler orders newest-first and caps the result at the requested limit.
/// <para>
/// Contract pinned for the implementer (T237):
/// <list type="bullet">
///   <item><c>VerbatimSampler</c> lives in <c>Application/Report/</c> and is pure.</item>
///   <item><c>IReadOnlyList&lt;VerbatimResponse&gt; Sample(IReadOnlyList&lt;VerbatimResponse&gt; responses,
///   int limit)</c> — orders by <c>SubmittedAt</c> descending (newest first) and returns at most
///   <paramref name="limit"/> items.</item>
///   <item><c>VerbatimResponse(Guid ResponseId, string Channel, DateTimeOffset SubmittedAt, string Text)</c>
///   lives in <c>Application/Report/</c>.</item>
/// </list>
/// </para>
/// </summary>
public sealed class VerbatimSamplerTests
{
    private readonly VerbatimSampler _sampler = new();

    private static readonly DateTimeOffset Anchor = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    // Builds `count` responses whose SubmittedAt increases by one hour per index (index 0 = oldest).
    private static IReadOnlyList<VerbatimResponse> BuildResponses(int count) =>
        Enumerable.Range(0, count)
            .Select(i => new VerbatimResponse(Guid.NewGuid(), "email", Anchor.AddHours(i), $"comment {i}"))
            .ToList();

    [Fact]
    public void Sample_caps_the_result_at_the_limit_and_returns_the_newest_first()
    {
        var responses = BuildResponses(150); // index 149 is the newest

        var sample = _sampler.Sample(responses, limit: 100);

        sample.Should().HaveCount(100);
        sample[0].Should().Be(responses[149]);   // newest first
        sample[99].Should().Be(responses[50]);   // 100th newest — everything older than index 50 dropped
        sample.Should().BeInDescendingOrder(r => r.SubmittedAt);
    }

    [Fact]
    public void Sample_returns_every_response_newest_first_when_fewer_than_the_limit()
    {
        var responses = BuildResponses(3);

        var sample = _sampler.Sample(responses, limit: 100);

        sample.Should().HaveCount(3);
        sample.Should().ContainInOrder(responses[2], responses[1], responses[0]);
    }

    [Fact]
    public void Sample_returns_an_empty_list_when_there_are_no_responses()
    {
        _sampler.Sample(Array.Empty<VerbatimResponse>(), limit: 100).Should().BeEmpty();
    }
}
