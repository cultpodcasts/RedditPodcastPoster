using AutoFixture;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Discovery.Repositories;
using RedditPodcastPoster.Models.Discovery;
using Xunit;

namespace RedditPodcastPoster.Discovery.Tests.Repositories;

public class DiscoveryResultsRepositoryStateTests
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private DiscoveryResultsDocument _stored;

    public DiscoveryResultsRepositoryStateTests()
    {
        _stored = new DiscoveryResultsDocument(DateTime.UtcNow, []) { SearchSince = _fixture.Create<string>() };
        var response = _mocker.GetMock<ItemResponse<DiscoveryResultsDocument>>();
        response.SetupGet(r => r.Resource).Returns(() => _stored);
        response.SetupGet(r => r.ETag).Returns(_fixture.Create<string>());
        _mocker.GetMock<Container>()
            .Setup(c => c.ReadItemAsync<DiscoveryResultsDocument>(It.IsAny<string>(), It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => response.Object);
    }

    [Fact(DisplayName =
        "INVARIANT: SetProcessed leaves a report awaiting supervisor review untouched, because only a supervisor applying the held submission may promote it to Processed.")]
    public async Task set_processed_skips_pending_supervisor()
    {
        // Arrange
        _stored.State = DiscoveryResultsDocumentState.PendingSupervisor;
        var sut = _mocker.CreateInstance<DiscoveryResultsRepository>();

        // Act
        await sut.SetProcessed([_stored.Id]);

        // Assert
        _stored.State.Should().Be(DiscoveryResultsDocumentState.PendingSupervisor);
        _mocker.GetMock<Container>().Verify(c => c.UpsertItemAsync(It.IsAny<DiscoveryResultsDocument>(),
            It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "SetProcessed still marks an Unprocessed report Processed, because unsupervised processing is unchanged.")]
    public async Task set_processed_marks_unprocessed()
    {
        // Arrange
        var sut = _mocker.CreateInstance<DiscoveryResultsRepository>();

        // Act
        await sut.SetProcessed([_stored.Id]);

        // Assert
        _stored.State.Should().Be(DiscoveryResultsDocumentState.Processed);
    }

    [Fact(DisplayName =
        "TryTransitionState refuses when the report is not in the expected from-state and writes nothing, because a held or processed report must not be re-held or re-processed.")]
    public async Task transition_refuses_wrong_from_state()
    {
        // Arrange
        _stored.State = DiscoveryResultsDocumentState.Processed;
        var sut = _mocker.CreateInstance<DiscoveryResultsRepository>();

        // Act
        var moved = await sut.TryTransitionState(_stored.Id, DiscoveryResultsDocumentState.Unprocessed,
            DiscoveryResultsDocumentState.PendingSupervisor);

        // Assert
        moved.Should().BeFalse();
        _mocker.GetMock<Container>().Verify(c => c.ReplaceItemAsync(It.IsAny<DiscoveryResultsDocument>(),
            It.IsAny<string>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
