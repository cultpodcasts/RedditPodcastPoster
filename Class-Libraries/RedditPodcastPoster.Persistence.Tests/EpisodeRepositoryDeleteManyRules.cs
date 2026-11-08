using System.Collections.Concurrent;
using System.Net;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Persistence.Repositories;

namespace RedditPodcastPoster.Persistence.Tests;

public class EpisodeRepositoryDeleteManyRules
{
    private const double ChargePerDelete = 5.5;
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly Podcast _podcast;
    private readonly List<string> _deletedIds = [];
    private readonly HashSet<string> _missingIds = [];
    private readonly HashSet<string> _failingIds = [];
    private readonly ConcurrentDictionary<string, int> _throttleOnce = new();
    private int _deleteCalls;
    private int _inFlight;
    private int _maxInFlight;

    public EpisodeRepositoryDeleteManyRules()
    {
        _podcast = _fixture.CreatePodcast();
        var response = Mock.Of<ItemResponse<Episode>>(r => r.RequestCharge == ChargePerDelete);
        _mocker.GetMock<Container>()
            .Setup(c => c.DeleteItemAsync<Episode>(
                It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, PartitionKey, ItemRequestOptions, CancellationToken>(async (id, _, _, _) =>
            {
                Interlocked.Increment(ref _deleteCalls);
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

                if (_throttleOnce.TryRemove(id, out _))
                {
                    throw new CosmosException("synthetic", HttpStatusCode.TooManyRequests, 0, string.Empty, 0);
                }

                lock (_deletedIds)
                {
                    _deletedIds.Add(id);
                }

                return response;
            });
    }

    private Episode[] CreateEpisodes(int count, Action<Episode>? customize = null) =>
        Enumerable.Range(0, count).Select(_ => _fixture.CreateStoredEpisode(_podcast, customize)).ToArray();

    [Fact(DisplayName =
        "Bulk deleting 1000 episodes makes exactly one Cosmos delete per episode with bounded parallelism, no " +
        "per-episode read and no podcast read or save, and reports the total request charge, where per-episode " +
        "Delete made four to six sequential round trips each.")]
    public async Task bulk_delete_is_one_round_trip_per_episode()
    {
        // Arrange
        var episodes = CreateEpisodes(990).Concat(CreateEpisodes(10, e => e.Removed = true)).ToArray();
        var sut = _mocker.CreateInstance<EpisodeRepository>();

        // Act
        var result = await sut.DeleteMany(_podcast.Id, episodes);

        // Assert
        result.Should().Be(new EpisodeBulkDeleteResult(1000, 0, 1000 * ChargePerDelete, 0));
        _deletedIds.Should().BeEquivalentTo(episodes.Select(e => e.Id.ToString()));
        _maxInFlight.Should().BeGreaterThan(1).And.BeLessThanOrEqualTo(CatalogueBulkWrite.Parallelism);
        _mocker.GetMock<Container>().Verify(c => c.ReadItemAsync<Episode>(
            It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<IPodcastRepository>().VerifyNoOtherCalls();
    }

    [Fact(DisplayName =
        "A podcast kind transfer leaves the home page active count unchanged: the bulk delete never patches the " +
        "counter, because the count also includes the news reports and TV show episodes the transfer writes.")]
    public async Task transfer_leaves_home_page_count_unchanged()
    {
        // Arrange
        var episodes = CreateEpisodes(25);
        _failingIds.Add(episodes[3].Id.ToString());
        var sut = _mocker.CreateInstance<EpisodeRepository>();
        await sut.DeleteMany(_podcast.Id, episodes.Skip(4).ToArray());

        // Act
        var act = () => sut.DeleteMany(_podcast.Id, episodes.Take(4).ToArray());

        // Assert
        await act.Should().ThrowAsync<CosmosException>();
        _mocker.GetMock<ILookupRepository>().Verify(
            l => l.IncrementHomePageActiveEpisodeCount(It.IsAny<int>()), Times.Never);
        _mocker.GetMock<ILookupRepository>().VerifyNoOtherCalls();
    }

    [Fact(DisplayName =
        "Episodes already deleted by an earlier attempt are skipped and reported as missing, so a resumed transfer " +
        "goes on with the rest.")]
    public async Task missing_episodes_are_skipped()
    {
        // Arrange
        var episodes = CreateEpisodes(4);
        _missingIds.Add(episodes[0].Id.ToString());
        _missingIds.Add(episodes[1].Id.ToString());
        var sut = _mocker.CreateInstance<EpisodeRepository>();

        // Act
        var result = await sut.DeleteMany(_podcast.Id, episodes);

        // Assert
        result.Deleted.Should().Be(2);
        result.Missing.Should().Be(2);
        _deletedIds.Should().BeEquivalentTo([episodes[2].Id.ToString(), episodes[3].Id.ToString()]);
    }

    [Fact(DisplayName =
        "A delete throttled (429) after the SDK's own retries is retried and the row is deleted, and the retry is " +
        "counted, so one hot partition on the serverless account does not abort a transfer part way.")]
    public async Task throttled_delete_is_retried()
    {
        // Arrange
        var episodes = CreateEpisodes(3);
        _throttleOnce[episodes[1].Id.ToString()] = 1;
        var sut = _mocker.CreateInstance<EpisodeRepository>();

        // Act
        var result = await sut.DeleteMany(_podcast.Id, episodes);

        // Assert
        result.Deleted.Should().Be(3);
        result.ThrottleRetries.Should().Be(1);
        _deletedIds.Should().BeEquivalentTo(episodes.Select(e => e.Id.ToString()));
    }

    [Fact(DisplayName =
        "A batch that mixes this podcast's episodes with one of another podcast is refused before any delete, " +
        "because the bulk delete targets one podcast partition and a refused call must change nothing.")]
    public async Task episode_of_other_podcast_is_refused()
    {
        // Arrange
        var own = CreateEpisodes(20);
        var stranger = _fixture.CreateStoredEpisode(_fixture.CreatePodcast());
        var batch = own.Take(10).Append(stranger).Concat(own.Skip(10)).ToArray();
        var sut = _mocker.CreateInstance<EpisodeRepository>();

        // Act
        var act = () => sut.DeleteMany(_podcast.Id, batch);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _deleteCalls.Should().Be(0);
        _deletedIds.Should().BeEmpty();
    }
}
