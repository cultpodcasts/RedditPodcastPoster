using System.Net;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Persistence.Repositories;

namespace RedditPodcastPoster.Persistence.Tests;

public class EpisodeRepositoryParentTransferDeleteRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly Podcast _podcast;
    private readonly List<string> _deletedIds = [];
    private readonly HashSet<string> _missingIds = [];
    private readonly HashSet<string> _failingIds = [];
    private int _inFlight;
    private int _maxInFlight;

    public EpisodeRepositoryParentTransferDeleteRules()
    {
        _podcast = _fixture.CreatePodcast();
        _mocker.GetMock<Container>()
            .Setup(c => c.DeleteItemAsync<Episode>(
                It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, PartitionKey, ItemRequestOptions, CancellationToken>(async (id, _, _, _) =>
            {
                var now = Interlocked.Increment(ref _inFlight);
                int seen;
                do
                {
                    seen = _maxInFlight;
                } while (now > seen && Interlocked.CompareExchange(ref _maxInFlight, now, seen) != seen);

                await Task.Delay(1);
                Interlocked.Decrement(ref _inFlight);
                if (_missingIds.Contains(id))
                {
                    throw new CosmosException("synthetic", HttpStatusCode.NotFound, 0, string.Empty, 0);
                }

                if (_failingIds.Contains(id))
                {
                    throw new CosmosException("synthetic", HttpStatusCode.ServiceUnavailable, 0, string.Empty, 0);
                }

                lock (_deletedIds)
                {
                    _deletedIds.Add(id);
                }

                return Mock.Of<ItemResponse<Episode>>();
            });
    }

    private Episode[] CreateEpisodes(int count, Action<Episode>? customize = null) =>
        Enumerable.Range(0, count).Select(_ => _fixture.CreateStoredEpisode(_podcast, customize)).ToArray();

    [Fact(DisplayName =
        "A transfer bulk delete of 1000 episodes makes exactly one Cosmos delete per episode with bounded " +
        "parallelism, no per-episode read, no podcast read or save, and one home page counter patch, where the old " +
        "per-episode Delete made four to six sequential round trips each.")]
    public async Task bulk_delete_is_one_round_trip_per_episode_and_one_counter_patch()
    {
        // Arrange
        var counted = CreateEpisodes(990);
        var removed = CreateEpisodes(10, e => e.Removed = true);
        var episodes = counted.Concat(removed).ToArray();
        var sut = _mocker.CreateInstance<EpisodeRepository>();

        // Act
        var deleted = await sut.DeleteForParentTransfer(_podcast.Id, episodes);

        // Assert
        deleted.Should().Be(1000);
        _deletedIds.Should().BeEquivalentTo(episodes.Select(e => e.Id.ToString()));
        _maxInFlight.Should().BeGreaterThan(1).And.BeLessThanOrEqualTo(EpisodeRepository.ParentTransferDeleteParallelism);
        _mocker.GetMock<Container>().Verify(c => c.ReadItemAsync<Episode>(
            It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<IPodcastRepository>().VerifyNoOtherCalls();
        _mocker.GetMock<ILookupRepository>().Verify(l => l.IncrementHomePageActiveEpisodeCount(-990), Times.Once);
        _mocker.GetMock<ILookupRepository>().VerifyNoOtherCalls();
    }

    [Fact(DisplayName =
        "Episodes already deleted by an earlier attempt are skipped and not counted, so a resumed transfer does " +
        "not decrement the home page count twice.")]
    public async Task missing_episodes_are_skipped_and_not_counted()
    {
        // Arrange
        var episodes = CreateEpisodes(4);
        _missingIds.Add(episodes[0].Id.ToString());
        _missingIds.Add(episodes[1].Id.ToString());
        var sut = _mocker.CreateInstance<EpisodeRepository>();

        // Act
        var deleted = await sut.DeleteForParentTransfer(_podcast.Id, episodes);

        // Assert
        deleted.Should().Be(2);
        _mocker.GetMock<ILookupRepository>().Verify(l => l.IncrementHomePageActiveEpisodeCount(-2), Times.Once);
    }

    [Fact(DisplayName =
        "When a delete fails, the error surfaces and the home page count is still decremented once for the " +
        "counted episodes that were deleted, because those rows are gone either way.")]
    public async Task failure_still_applies_counter_for_deleted_rows()
    {
        // Arrange
        var episodes = CreateEpisodes(3);
        _failingIds.Add(episodes[1].Id.ToString());
        var sut = _mocker.CreateInstance<EpisodeRepository>();

        // Act
        var act = () => sut.DeleteForParentTransfer(_podcast.Id, episodes);

        // Assert
        await act.Should().ThrowAsync<CosmosException>();
        _mocker.GetMock<ILookupRepository>().Verify(
            l => l.IncrementHomePageActiveEpisodeCount(-_deletedIds.Count), Times.Once);
    }

    [Fact(DisplayName =
        "An episode of another podcast in the batch is refused, because the bulk delete targets one podcast partition.")]
    public async Task episode_of_other_podcast_is_refused()
    {
        // Arrange
        var stranger = _fixture.CreateStoredEpisode(_fixture.CreatePodcast());
        var sut = _mocker.CreateInstance<EpisodeRepository>();

        // Act
        var act = () => sut.DeleteForParentTransfer(_podcast.Id, [stranger]);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _deletedIds.Should().BeEmpty();
    }
}
