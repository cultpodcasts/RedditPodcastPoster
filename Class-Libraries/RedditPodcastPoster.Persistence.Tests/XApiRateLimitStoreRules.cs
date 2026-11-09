using System.Net;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Social;
using RedditPodcastPoster.Persistence.Lookups;

namespace RedditPodcastPoster.Persistence.Tests;

public class XApiRateLimitStoreRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    private static CosmosException CosmosStatus(HttpStatusCode status) => new("synthetic", status, 0, string.Empty, 0);

    [Fact(DisplayName =
        "When no X rate-limit state has been saved, then reading it returns null, because a missing document means X is not blocked.")]
    public async Task missing_state_reads_as_null()
    {
        // Arrange
        _mocker.GetMock<Container>()
            .Setup(x => x.ReadItemAsync<XApiRateLimitState>(It.IsAny<string>(), It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(CosmosStatus(HttpStatusCode.NotFound));
        var sut = _mocker.CreateInstance<XApiRateLimitStore>();

        // Act
        var state = await sut.GetAsync();

        // Assert
        state.Should().BeNull();
    }

    [Fact(DisplayName =
        "When the X rate-limit state is saved, then it is upserted into LookUps under its fixed singleton id as the partition key.")]
    public async Task save_upserts_singleton_by_fixed_id()
    {
        // Arrange
        var state = new XApiRateLimitState
        {
            RateLimitResetUtc = DomainTestFixture.UtcAtTime(0, TimeSpan.FromHours(14))
        };
        var sut = _mocker.CreateInstance<XApiRateLimitStore>();

        // Act
        await sut.SaveAsync(state);

        // Assert
        _mocker.GetMock<Container>().Verify(x => x.UpsertItemAsync(
            state,
            new PartitionKey(XApiRateLimitState._Id.ToString()),
            It.IsAny<ItemRequestOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "When an X rate-limit state with a different id is saved, then the save is refused, because there is only one skip-window document.")]
    public async Task save_refuses_other_ids()
    {
        // Arrange
        var state = new XApiRateLimitState { Id = _fixture.CreateGuid() };
        var sut = _mocker.CreateInstance<XApiRateLimitStore>();

        // Act
        var act = () => sut.SaveAsync(state);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
        _mocker.GetMock<Container>().Verify(x => x.UpsertItemAsync(It.IsAny<XApiRateLimitState>(),
            It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
