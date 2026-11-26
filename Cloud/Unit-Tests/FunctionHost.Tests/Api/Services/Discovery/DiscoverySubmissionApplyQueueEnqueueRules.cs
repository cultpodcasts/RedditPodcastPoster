using Api.Models;
using Api.Services.Discovery;
using FluentAssertions;
using FunctionHost.Tests.Api.Services.Discovery.Fakes;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Discovery;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Discovery;

public class DiscoverySubmissionApplyQueueEnqueueRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<string> _order = [];
    private readonly NamedQueueClientResolver _queues;

    public DiscoverySubmissionApplyQueueEnqueueRules()
    {
        _queues = new NamedQueueClientResolver(_order);
        _mocker.Resolvers.Insert(0, _queues);
    }

    [Fact(DisplayName =
        "Enqueue ensures the apply queue and the poison queue exist, then sends the claimed submission on the " +
        "apply queue, so a missing poison queue is created by the injected client before the message is sent.")]
    public async Task enqueue_ensures_both_queues_then_sends_on_the_apply_queue()
    {
        // Arrange
        var submissionId = _fixture.CreateGuid();
        var message = new DiscoverySubmissionApplyQueueMessage
        {
            SubmissionId = submissionId,
            Operation = DiscoverySubmissionOperation.Apply,
            InProgressSince = DomainTestFixture.UtcToday,
            EnqueuedUtc = DomainTestFixture.UtcToday
        };
        var sut = _mocker.CreateInstance<DiscoverySubmissionApplyQueue>();

        // Act
        await sut.EnqueueAsync(message, CancellationToken.None);

        // Assert
        _order.Should().Equal("apply:exists", "poison:exists", "apply:send");
        _queues.Apply.LastMessage.Should().Contain(submissionId.ToString());
        _queues.Poison.LastMessage.Should().BeNull();
    }
}
