using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Episode;
using RedditPodcastPoster.PodcastServices.YouTube.Handlers;
using RedditPodcastPoster.PodcastServices.YouTube.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Handlers;

/// <summary>
/// Indexer YouTube retrieval must keep YouTubePlaylistQueryIsExpensive in step with the playlist-order
/// probe, because the stored flag chooses the pagination strategy for the following pass.
/// </summary>
public class YouTubeEpisodeRetrievalHandlerRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Across successive indexer passes a flipping playlist flips YouTubePlaylistQueryIsExpensive each time " +
        "because a playlist that switches order must switch pagination strategy on the following pass.")]
    public async Task Expensive_flag_round_trips_across_indexer_passes()
    {
        // Arrange — probes: oldest-first, newest-first, oldest-first
        var podcast = CreatePlaylistPodcast();
        var requestedExpensiveFlags = new List<bool>();
        var sut = CreateSut(requestedExpensiveFlags, true, false, true);

        // Act
        var storedFlags = await RunPasses(sut, podcast, passes: 3);

        // Assert — each pass requests using the flag as it stood before that pass's probe
        storedFlags.Should().Equal(true, false, true);
        requestedExpensiveFlags.Should().Equal(false, true, false);
    }

    [Fact(DisplayName =
        "An inconclusive probe between two conclusive probes leaves YouTubePlaylistQueryIsExpensive intact " +
        "because a pass that could not measure order must not reset the playlist pagination strategy.")]
    public async Task Inconclusive_pass_preserves_flag_between_flips()
    {
        // Arrange — probes: oldest-first, inconclusive, newest-first
        var podcast = CreatePlaylistPodcast();
        var requestedExpensiveFlags = new List<bool>();
        var sut = CreateSut(requestedExpensiveFlags, true, null, false);

        // Act
        var storedFlags = await RunPasses(sut, podcast, passes: 3);

        // Assert
        storedFlags.Should().Equal(true, true, false);
        requestedExpensiveFlags.Should().Equal(false, true, true);
    }

    [Fact(DisplayName =
        "When the podcast declares an arbitrary YouTube playlist order, retrieval forwards Arbitrary to the " +
        "provider and leaves YouTubePlaylistQueryIsExpensive untouched because curated playlists have no " +
        "positional order to probe.")]
    public async Task Arbitrary_order_forwards_playlist_order_and_leaves_expensive_flag_untouched()
    {
        // Arrange
        var podcast = CreatePlaylistPodcast();
        podcast.YouTubePlaylistOrder = PlaylistOrder.Arbitrary;
        podcast.YouTubePlaylistQueryIsExpensive = false;
        PlaylistOrder? forwardedOrder = null;
        var provider = new Mock<IYouTubeEpisodeProvider>();
        provider
            .Setup(x => x.GetPlaylistEpisodes(
                It.IsAny<YouTubePlaylistId>(),
                It.IsAny<YouTubeChannelId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<PlaylistOrder?>()))
            .Callback<YouTubePlaylistId, YouTubeChannelId?, IndexingContext, bool, PlaylistOrder?>(
                (_, _, _, _, playlistOrder) => forwardedOrder = playlistOrder)
            .ReturnsAsync(new GetPlaylistEpisodesResponse([], IsExpensiveQuery: true));
        var sut = new YouTubeEpisodeRetrievalHandler(
            provider.Object,
            NullLogger<YouTubeEpisodeRetrievalHandler>.Instance);

        // Act
        var result = await sut.GetEpisodes(
            podcast,
            [],
            new IndexingContext(DomainTestFixture.UtcDaysAgo(2), SkipExpensiveYouTubeQueries: true));

        // Assert
        result.Handled.Should().BeTrue();
        forwardedOrder.Should().Be(PlaylistOrder.Arbitrary);
        podcast.YouTubePlaylistQueryIsExpensive.Should().BeFalse(
            "arbitrary-order walks never apply the expensive-query flag from a head-order probe");
    }

    [Fact(DisplayName =
        "When the podcast declares an arbitrary YouTube playlist order and SkipExpensiveYouTubeQueries is set, " +
        "retrieval still calls the playlist provider because the full walk is the only correct read of a " +
        "curated playlist and is not gated by the expensive-query skip.")]
    public async Task Arbitrary_order_still_calls_playlist_provider_when_expensive_queries_skipped()
    {
        // Arrange
        var podcast = CreatePlaylistPodcast();
        podcast.YouTubePlaylistOrder = PlaylistOrder.Arbitrary;
        podcast.YouTubePlaylistQueryIsExpensive = true;
        var provider = new Mock<IYouTubeEpisodeProvider>();
        provider
            .Setup(x => x.GetPlaylistEpisodes(
                It.IsAny<YouTubePlaylistId>(),
                It.IsAny<YouTubeChannelId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<PlaylistOrder?>()))
            .ReturnsAsync(new GetPlaylistEpisodesResponse([], IsExpensiveQuery: null));
        var sut = new YouTubeEpisodeRetrievalHandler(
            provider.Object,
            NullLogger<YouTubeEpisodeRetrievalHandler>.Instance);

        // Act
        var result = await sut.GetEpisodes(
            podcast,
            [],
            new IndexingContext(DomainTestFixture.UtcDaysAgo(2), SkipExpensiveYouTubeQueries: true));

        // Assert
        result.Handled.Should().BeTrue();
        provider.Verify(
            x => x.GetPlaylistEpisodes(
                It.IsAny<YouTubePlaylistId>(),
                It.IsAny<YouTubeChannelId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                PlaylistOrder.Arbitrary),
            Times.Once);
    }

    private Podcast CreatePlaylistPodcast() =>
        _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.YouTubePlaylistId = _fixture.CreateYouTubePlaylistId();
            p.YouTubePlaylistQueryIsExpensive = false;
        });

    private static async Task<List<bool?>> RunPasses(
        YouTubeEpisodeRetrievalHandler sut,
        Podcast podcast,
        int passes)
    {
        var storedFlags = new List<bool?>();
        for (var pass = 0; pass < passes; pass++)
        {
            await sut.GetEpisodes(
                podcast,
                [],
                new IndexingContext(DomainTestFixture.UtcDaysAgo(2), SkipExpensiveYouTubeQueries: false));
            storedFlags.Add(podcast.YouTubePlaylistQueryIsExpensive);
        }

        return storedFlags;
    }

    /// <summary>
    /// Yields one playlist-order probe per pass and records the expensive-query flag each request carried,
    /// so a test can observe the stored flag feeding back into the following pass.
    /// </summary>
    private static YouTubeEpisodeRetrievalHandler CreateSut(
        List<bool> requestedExpensiveFlags,
        params bool?[] probes)
    {
        var remainingProbes = new Queue<bool?>(probes);
        var provider = new Mock<IYouTubeEpisodeProvider>();
        provider
            .Setup(x => x.GetPlaylistEpisodes(
                It.IsAny<YouTubePlaylistId>(),
                It.IsAny<YouTubeChannelId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<PlaylistOrder?>()))
            .Callback<YouTubePlaylistId, YouTubeChannelId?, IndexingContext, bool, PlaylistOrder?>(
                (_, _, _, expensivePlaylist, _) => requestedExpensiveFlags.Add(expensivePlaylist))
            .ReturnsAsync(() => new GetPlaylistEpisodesResponse([], remainingProbes.Dequeue()));

        return new YouTubeEpisodeRetrievalHandler(
            provider.Object,
            NullLogger<YouTubeEpisodeRetrievalHandler>.Instance);
    }
}
