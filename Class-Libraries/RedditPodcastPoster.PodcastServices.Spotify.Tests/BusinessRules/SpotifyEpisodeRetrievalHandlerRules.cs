using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Providers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules;

/// <summary>
/// Indexer Spotify retrieval must not call the provider when the podcast has no Spotify show id.
/// </summary>
public class SpotifyEpisodeRetrievalHandlerRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When the podcast has an empty SpotifyId, GetEpisodes returns not-handled with no episodes and does not call the provider " +
        "because hourly indexing only fetches catalogue rows for shows with a known Spotify id.")]
    public async Task Empty_spotify_id_does_not_call_provider()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.SpotifyId = string.Empty);
        var provider = new Mock<ISpotifyEpisodeProvider>(MockBehavior.Strict);
        var sut = new SpotifyEpisodeRetrievalHandler(
            provider.Object,
            NullLogger<SpotifyEpisodeRetrievalHandler>.Instance);

        // Act
        var result = await sut.GetEpisodes(podcast, new IndexingContext(SkipPodcastDiscovery: true));

        // Assert
        result.Handled.Should().BeFalse();
        result.Episodes.Should().BeEmpty();
        provider.Verify(
            x => x.GetEpisodes(It.IsAny<GetEpisodesRequest>(), It.IsAny<IndexingContext>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When the provider reports ExpensiveQueryFound, the handler sets SpotifyEpisodesQueryIsExpensive on the podcast " +
        "because subsequent indexer passes must use the ascending end-jump path for that show.")]
    public async Task Expensive_query_found_sets_podcast_flag()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.SpotifyId = _fixture.CreateSpotifyId();
            p.SpotifyEpisodesQueryIsExpensive = false;
        });
        var provider = new Mock<ISpotifyEpisodeProvider>();
        provider
            .Setup(x => x.GetEpisodes(It.IsAny<GetEpisodesRequest>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync(new GetEpisodesResponse([], ExpensiveQueryFound: true));
        var sut = new SpotifyEpisodeRetrievalHandler(
            provider.Object,
            NullLogger<SpotifyEpisodeRetrievalHandler>.Instance);

        // Act
        var result = await sut.GetEpisodes(podcast, new IndexingContext(SkipPodcastDiscovery: true));

        // Assert
        result.Handled.Should().BeTrue();
        podcast.SpotifyEpisodesQueryIsExpensive.Should().BeTrue();
        provider.Verify(
            x => x.GetEpisodes(
                It.Is<GetEpisodesRequest>(r =>
                    r.SpotifyPodcastId.PodcastId == podcast.SpotifyId &&
                    r.HasExpensiveSpotifyEpisodesQuery == false),
                It.IsAny<IndexingContext>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "When the provider reports a newest-first catalogue, the handler clears SpotifyEpisodesQueryIsExpensive " +
        "because Spotify shows are known to flip back from ascending order.")]
    public async Task Newest_first_probe_clears_podcast_flag()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.SpotifyId = _fixture.CreateSpotifyId();
            p.SpotifyEpisodesQueryIsExpensive = true;
        });
        var provider = new Mock<ISpotifyEpisodeProvider>();
        provider
            .Setup(x => x.GetEpisodes(It.IsAny<GetEpisodesRequest>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync(new GetEpisodesResponse([], ExpensiveQueryFound: false));
        var sut = new SpotifyEpisodeRetrievalHandler(
            provider.Object,
            NullLogger<SpotifyEpisodeRetrievalHandler>.Instance);

        // Act
        await sut.GetEpisodes(podcast, new IndexingContext(SkipPodcastDiscovery: true));

        // Assert
        podcast.SpotifyEpisodesQueryIsExpensive.Should().BeFalse();
    }

    [Fact(DisplayName =
        "Across successive indexer passes a flipping catalogue flips SpotifyEpisodesQueryIsExpensive each time and the stored flag drives the next request " +
        "because a show that switches order must switch pagination strategy on the following pass.")]
    public async Task Expensive_flag_round_trips_across_indexer_passes()
    {
        // Arrange — probes: ascending, newest-first, ascending
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.SpotifyId = _fixture.CreateSpotifyId();
            p.SpotifyEpisodesQueryIsExpensive = false;
        });
        var provider = new SequencedSpotifyEpisodeProvider(true, false, true);
        var sut = new SpotifyEpisodeRetrievalHandler(
            provider,
            NullLogger<SpotifyEpisodeRetrievalHandler>.Instance);

        // Act
        var storedFlags = new List<bool?>();
        for (var pass = 0; pass < 3; pass++)
        {
            await sut.GetEpisodes(podcast, new IndexingContext(SkipPodcastDiscovery: true));
            storedFlags.Add(podcast.SpotifyEpisodesQueryIsExpensive);
        }

        // Assert — each pass requests using the flag as it stood before that pass's probe
        storedFlags.Should().Equal(true, false, true);
        provider.RequestedExpensiveFlags.Should().Equal(false, true, false);
    }

    [Fact(DisplayName =
        "An inconclusive probe between two conclusive probes leaves SpotifyEpisodesQueryIsExpensive intact " +
        "because a pass that could not measure order must not reset the show's pagination strategy.")]
    public async Task Inconclusive_pass_preserves_flag_between_flips()
    {
        // Arrange — probes: ascending, inconclusive, newest-first
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.SpotifyId = _fixture.CreateSpotifyId();
            p.SpotifyEpisodesQueryIsExpensive = false;
        });
        var provider = new SequencedSpotifyEpisodeProvider(true, null, false);
        var sut = new SpotifyEpisodeRetrievalHandler(
            provider,
            NullLogger<SpotifyEpisodeRetrievalHandler>.Instance);

        // Act
        var storedFlags = new List<bool?>();
        for (var pass = 0; pass < 3; pass++)
        {
            await sut.GetEpisodes(podcast, new IndexingContext(SkipPodcastDiscovery: true));
            storedFlags.Add(podcast.SpotifyEpisodesQueryIsExpensive);
        }

        // Assert
        storedFlags.Should().Equal(true, true, false);
        provider.RequestedExpensiveFlags.Should().Equal(false, true, true);
    }

    [Fact(DisplayName =
        "When SkipSpotifyUrlResolving is set, GetEpisodes still calls the provider but returns Handled=false even if episodes are returned " +
        "because rate-limit recovery must not mark Spotify as fully handled for the indexer pass.")]
    public async Task Skip_spotify_url_resolving_returns_not_handled()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.SpotifyId = _fixture.CreateSpotifyId());
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        var provider = new Mock<ISpotifyEpisodeProvider>();
        provider
            .Setup(x => x.GetEpisodes(It.IsAny<GetEpisodesRequest>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync(new GetEpisodesResponse([episode], ExpensiveQueryFound: false));
        var sut = new SpotifyEpisodeRetrievalHandler(
            provider.Object,
            NullLogger<SpotifyEpisodeRetrievalHandler>.Instance);

        // Act
        var result = await sut.GetEpisodes(
            podcast,
            new IndexingContext { SkipSpotifyUrlResolving = true });

        // Assert
        result.Handled.Should().BeFalse();
        result.Episodes.Should().ContainSingle();
        provider.Verify(
            x => x.GetEpisodes(It.IsAny<GetEpisodesRequest>(), It.IsAny<IndexingContext>()),
            Times.Once);
    }

    /// <summary>
    /// Returns one catalogue-order probe per call and records the expensive-query flag each request
    /// carried, so a test can observe the flag feeding back into the following pass.
    /// </summary>
    private sealed class SequencedSpotifyEpisodeProvider(params bool?[] probes) : ISpotifyEpisodeProvider
    {
        private int _call;

        public List<bool> RequestedExpensiveFlags { get; } = [];

        public Task<GetEpisodesResponse> GetEpisodes(
            GetEpisodesRequest request,
            IndexingContext indexingContext)
        {
            RequestedExpensiveFlags.Add(request.HasExpensiveSpotifyEpisodesQuery);
            var probe = probes[Math.Min(_call++, probes.Length - 1)];
            return Task.FromResult(new GetEpisodesResponse([], probe));
        }
    }
}
