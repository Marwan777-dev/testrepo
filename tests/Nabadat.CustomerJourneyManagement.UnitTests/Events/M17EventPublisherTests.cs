using FluentAssertions;
using Nabadat.CustomerJourneyManagement.Application.Events;
using Nabadat.CustomerJourneyManagement.Application.Interfaces;
using NSubstitute;
using Xunit;

namespace Nabadat.CustomerJourneyManagement.UnitTests.Events;

/// <summary>
/// Unit tests for the M-16 → M-17 audit bridge (T020 / US-1): <see cref="M17EventPublisher"/>
/// and the <see cref="CustomerJourneyManagementEvent"/> typed factories. The publisher now tracks an <c>event_log</c>
/// row on the shared <see cref="ITenantDbContext"/> and saves; called inside the caller's
/// <c>ITenantDbContext.ExecuteAsync</c> the audit row commits or rolls back with the business
/// change (FR-015). (The end-to-end commit/rollback over a real database is proven by the
/// integration suite; here we verify the null-guard and the event payload is correct.)
/// </summary>
public sealed class M17EventPublisherTests
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PublishAsync_throws_when_event_is_null()
    {
        var publisher = new M17EventPublisher(Substitute.For<ITenantDbContext>());

        Func<Task> act = () => publisher.PublishAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void JourneyCreated_event_carries_correct_journeyId_and_event_type()
    {
        var journeyId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var actorId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();

        var evt = CustomerJourneyManagementEvent.JourneyCreated(actorId, "P-01", journeyId, OccurredAt, correlationId);

        evt.EventType.Should().Be(CustomerJourneyManagementEventTypes.JourneyCreated);
        CustomerJourneyManagementEventTypes.JourneyCreated.Should().Be("journey.created");
        evt.EntityType.Should().Be("journey");
        evt.EntityId.Should().Be(journeyId);
        evt.ActorId.Should().Be(actorId);
        evt.ActorPersona.Should().Be("P-01");
        evt.OccurredAtUtc.Should().Be(OccurredAt);
        evt.CorrelationId.Should().Be(correlationId);
        evt.OldValue.Should().BeNull();
    }
}
