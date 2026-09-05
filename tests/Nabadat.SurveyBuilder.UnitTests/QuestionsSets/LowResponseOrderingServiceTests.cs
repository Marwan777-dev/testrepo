using FluentAssertions;
using Nabadat.SurveyBuilder.Application.QuestionsSets;
using Nabadat.SurveyBuilder.Application.QuestionsSets.Dtos;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.QuestionsSets;

/// <summary>
/// T131 [US3] — unit tests for <c>LowResponseOrderingService</c> (FR-10.4, research.md §7). A pure
/// in-memory function over a <c>IReadOnlyDictionary&lt;Guid,long&gt; responseCounts</c> projection.
/// The cascade is Set → Section → Survey: within a set, pick the least-answered questions; a
/// section's ordering key is the lowest response count among its eligible questions; sections are
/// ordered ascending by that key (least-answered first).
/// <para>
/// Contract pinned for the implementer (T141):
/// <list type="bullet">
///   <item><c>LowResponseOrderingService</c> lives in <c>Application/QuestionsSets/</c> and is pure.</item>
///   <item><c>IReadOnlyList&lt;Guid&gt; OrderSections(IReadOnlyList&lt;OrderingSection&gt; sections,
///   IReadOnlyDictionary&lt;Guid,long&gt; responseCounts)</c> — returns section ids ordered by
///   ascending lowest-response question (least-answered section first).</item>
///   <item><c>IReadOnlyList&lt;Guid&gt; PickCandidates(OrderingSet set, int count,
///   IReadOnlyDictionary&lt;Guid,long&gt; responseCounts)</c> — returns the <c>count</c> least-answered
///   member question ids (ascending).</item>
///   <item><c>OrderingSection(Guid SectionId, IReadOnlyList&lt;Guid&gt; QuestionIds)</c> and
///   <c>OrderingSet(Guid SetId, IReadOnlyList&lt;Guid&gt; QuestionIds)</c> live in
///   <c>Application/QuestionsSets/Dtos/</c>.</item>
/// </list>
/// </para>
/// </summary>
public sealed class LowResponseOrderingServiceTests
{
    private readonly LowResponseOrderingService _service = new();

    [Fact]
    public void OrderSections_orders_sections_by_their_lowest_response_question_ascending()
    {
        // Three sections whose lowest-response questions are 7, 4, 12 → expect [section2, section1, section3].
        var s1Low = Guid.NewGuid();
        var s1High = Guid.NewGuid();
        var s2Low = Guid.NewGuid();
        var s2High = Guid.NewGuid();
        var s3Low = Guid.NewGuid();
        var s3High = Guid.NewGuid();

        var section1 = new OrderingSection(Guid.NewGuid(), new[] { s1Low, s1High });
        var section2 = new OrderingSection(Guid.NewGuid(), new[] { s2Low, s2High });
        var section3 = new OrderingSection(Guid.NewGuid(), new[] { s3Low, s3High });

        var responseCounts = new Dictionary<Guid, long>
        {
            [s1Low] = 7,
            [s1High] = 20,
            [s2Low] = 4,
            [s2High] = 9,
            [s3Low] = 12,
            [s3High] = 30,
        };

        var order = _service.OrderSections(new[] { section1, section2, section3 }, responseCounts);

        order.Should().Equal(section2.SectionId, section1.SectionId, section3.SectionId);
    }

    [Fact]
    public void PickCandidates_returns_the_count_least_answered_questions_ascending()
    {
        var qA = Guid.NewGuid(); // 10
        var qB = Guid.NewGuid(); // 2
        var qC = Guid.NewGuid(); // 8
        var qD = Guid.NewGuid(); // 1
        var qE = Guid.NewGuid(); // 5
        var set = new OrderingSet(Guid.NewGuid(), new[] { qA, qB, qC, qD, qE });

        var responseCounts = new Dictionary<Guid, long>
        {
            [qA] = 10,
            [qB] = 2,
            [qC] = 8,
            [qD] = 1,
            [qE] = 5,
        };

        var picked = _service.PickCandidates(set, count: 3, responseCounts);

        picked.Should().Equal(qD, qB, qE);
    }

    [Fact]
    public void PickCandidates_returns_every_member_when_count_meets_or_exceeds_the_set_size()
    {
        var q1 = Guid.NewGuid();
        var q2 = Guid.NewGuid();
        var set = new OrderingSet(Guid.NewGuid(), new[] { q1, q2 });
        var responseCounts = new Dictionary<Guid, long> { [q1] = 3, [q2] = 1 };

        var picked = _service.PickCandidates(set, count: 5, responseCounts);

        picked.Should().BeEquivalentTo(new[] { q1, q2 });
    }

    [Fact]
    public void PickCandidates_treats_a_missing_response_count_as_zero_least_answered()
    {
        var seen = Guid.NewGuid();     // answered 4 times
        var neverSeen = Guid.NewGuid(); // absent from the projection ⇒ 0 responses
        var set = new OrderingSet(Guid.NewGuid(), new[] { seen, neverSeen });
        var responseCounts = new Dictionary<Guid, long> { [seen] = 4 };

        var picked = _service.PickCandidates(set, count: 1, responseCounts);

        picked.Should().Equal(neverSeen);
    }
}
