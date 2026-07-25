using FluentAssertions;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Episode;
using RedditPodcastPoster.PodcastServices.YouTube.Handlers;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using EpisodeModel = RedditPodcastPoster.Models.Episodes.Episode;
using IYouTubeEpisodeProvider = RedditPodcastPoster.PodcastServices.YouTube.Episode.IYouTubeEpisodeProvider;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Handlers;

public class YouTubeEpisodeRetrievalHandlerTests
{
    [Fact]
    public async Task GetEpisodes_ChannelOnly_WhenSkipExpensiveYouTubeQueries_StillCallsChannelDiscovery()
    {
        var podcast = new Podcast
        {
            Id = Guid.NewGuid(),
            YouTubeChannelId = "UC_test_channel",
            YouTubePlaylistId = null!
        };
        var indexingContext = new IndexingContext(
            DateTime.UtcNow.AddDays(-2),
            SkipExpensiveYouTubeQueries: true);
        var expectedEpisodes = new List<EpisodeModel> { new() { Title = "episode-1" } };

        var youTubeEpisodeProvider = new Mock<IYouTubeEpisodeProvider>();
        youTubeEpisodeProvider
            .Setup(x => x.GetEpisodes(podcast, indexingContext, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(expectedEpisodes);

        var sut = new YouTubeEpisodeRetrievalHandler(
            youTubeEpisodeProvider.Object,
            NullLogger<YouTubeEpisodeRetrievalHandler>.Instance);

        var result = await sut.GetEpisodes(podcast, [], indexingContext);

        result.Handled.Should().BeTrue();
        result.Episodes.Should().BeEquivalentTo(expectedEpisodes);
        youTubeEpisodeProvider.Verify(
            x => x.GetEpisodes(podcast, indexingContext, It.IsAny<IEnumerable<string>>()),
            Times.Once);
        youTubeEpisodeProvider.Verify(
            x => x.GetPlaylistEpisodes(
                It.IsAny<YouTubePlaylistId>(),
                It.IsAny<YouTubeChannelId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task GetEpisodes_ExpensivePlaylist_WhenSkipExpensiveYouTubeQueries_UsesSinglePageFetch()
    {
        var podcast = new Podcast
        {
            Id = Guid.NewGuid(),
            YouTubeChannelId = "UC_test_channel",
            YouTubePlaylistId = "PL_test_playlist",
            YouTubePlaylistQueryIsExpensive = true
        };
        var indexingContext = new IndexingContext(
            DateTime.UtcNow.AddDays(-2),
            SkipExpensiveYouTubeQueries: true);
        var expectedEpisodes = new List<EpisodeModel> { new() { Title = "playlist-episode" } };

        var youTubeEpisodeProvider = new Mock<IYouTubeEpisodeProvider>();
        youTubeEpisodeProvider
            .Setup(x => x.GetPlaylistEpisodes(
                new YouTubePlaylistId("PL_test_playlist"),
                new YouTubeChannelId("UC_test_channel"),
                indexingContext,
                false))
            .ReturnsAsync(new GetPlaylistEpisodesResponse(expectedEpisodes));

        var sut = new YouTubeEpisodeRetrievalHandler(
            youTubeEpisodeProvider.Object,
            NullLogger<YouTubeEpisodeRetrievalHandler>.Instance);

        var result = await sut.GetEpisodes(podcast, [], indexingContext);

        result.Handled.Should().BeTrue();
        result.Episodes.Should().BeEquivalentTo(expectedEpisodes);
        youTubeEpisodeProvider.Verify(
            x => x.GetPlaylistEpisodes(
                new YouTubePlaylistId("PL_test_playlist"),
                new YouTubeChannelId("UC_test_channel"),
                indexingContext,
                false),
            Times.Once);
    }

    [Fact]
    public async Task GetEpisodes_ExpensivePlaylist_WhenExpensiveQueriesAllowed_UsesExpensivePagination()
    {
        var podcast = new Podcast
        {
            Id = Guid.NewGuid(),
            YouTubeChannelId = "UC_test_channel",
            YouTubePlaylistId = "PL_test_playlist",
            YouTubePlaylistQueryIsExpensive = true
        };
        var indexingContext = new IndexingContext(
            DateTime.UtcNow.AddDays(-2),
            SkipExpensiveYouTubeQueries: false);

        var youTubeEpisodeProvider = new Mock<IYouTubeEpisodeProvider>();
        youTubeEpisodeProvider
            .Setup(x => x.GetPlaylistEpisodes(
                new YouTubePlaylistId("PL_test_playlist"),
                new YouTubeChannelId("UC_test_channel"),
                indexingContext,
                true))
            .ReturnsAsync(new GetPlaylistEpisodesResponse([]));

        var sut = new YouTubeEpisodeRetrievalHandler(
            youTubeEpisodeProvider.Object,
            NullLogger<YouTubeEpisodeRetrievalHandler>.Instance);

        await sut.GetEpisodes(podcast, [], indexingContext);

        youTubeEpisodeProvider.Verify(
            x => x.GetPlaylistEpisodes(
                new YouTubePlaylistId("PL_test_playlist"),
                new YouTubeChannelId("UC_test_channel"),
                indexingContext,
                true),
            Times.Once);
    }

    [Fact(DisplayName =
        "Across successive indexer passes a flipping playlist flips YouTubePlaylistQueryIsExpensive each time " +
        "because a show that switches order must switch pagination strategy on the following pass.")]
    public async Task Expensive_flag_round_trips_across_indexer_passes()
    {
        var podcast = new Podcast
        {
            Id = Guid.NewGuid(),
            YouTubeChannelId = "UC_test_channel",
            YouTubePlaylistId = "PL_test_playlist",
            YouTubePlaylistQueryIsExpensive = false
        };
        var provider = new SequencedYouTubeEpisodeProvider(true, false, true);
        var sut = new YouTubeEpisodeRetrievalHandler(
            provider,
            NullLogger<YouTubeEpisodeRetrievalHandler>.Instance);

        var storedFlags = new List<bool?>();
        for (var pass = 0; pass < 3; pass++)
        {
            await sut.GetEpisodes(
                podcast,
                [],
                new IndexingContext(DateTime.UtcNow.AddDays(-2), SkipExpensiveYouTubeQueries: false));
            storedFlags.Add(podcast.YouTubePlaylistQueryIsExpensive);
        }

        storedFlags.Should().Equal(true, false, true);
        provider.RequestedExpensiveFlags.Should().Equal(false, true, false);
    }

    [Fact(DisplayName =
        "An inconclusive probe between two conclusive probes leaves YouTubePlaylistQueryIsExpensive intact " +
        "because a pass that could not measure order must not reset the playlist pagination strategy.")]
    public async Task Inconclusive_pass_preserves_flag_between_flips()
    {
        var podcast = new Podcast
        {
            Id = Guid.NewGuid(),
            YouTubeChannelId = "UC_test_channel",
            YouTubePlaylistId = "PL_test_playlist",
            YouTubePlaylistQueryIsExpensive = false
        };
        var provider = new SequencedYouTubeEpisodeProvider(true, null, false);
        var sut = new YouTubeEpisodeRetrievalHandler(
            provider,
            NullLogger<YouTubeEpisodeRetrievalHandler>.Instance);

        var storedFlags = new List<bool?>();
        for (var pass = 0; pass < 3; pass++)
        {
            await sut.GetEpisodes(
                podcast,
                [],
                new IndexingContext(DateTime.UtcNow.AddDays(-2), SkipExpensiveYouTubeQueries: false));
            storedFlags.Add(podcast.YouTubePlaylistQueryIsExpensive);
        }

        storedFlags.Should().Equal(true, true, false);
        provider.RequestedExpensiveFlags.Should().Equal(false, true, true);
    }

    /// <summary>
    /// Returns one playlist-order probe per call and records the expensive-query flag each request
    /// carried, so a test can observe the flag feeding back into the following pass.
    /// </summary>
    private sealed class SequencedYouTubeEpisodeProvider(params bool?[] probes) : IYouTubeEpisodeProvider
    {
        private int _call;

        public List<bool> RequestedExpensiveFlags { get; } = [];

        public Task<GetPlaylistEpisodesResponse> GetPlaylistEpisodes(
            YouTubePlaylistId youTubePlaylistId,
            YouTubeChannelId? youTubeChannelId,
            IndexingContext indexingContext,
            bool expensivePlaylist = false)
        {
            RequestedExpensiveFlags.Add(expensivePlaylist);
            var probe = probes[Math.Min(_call++, probes.Length - 1)];
            return Task.FromResult(new GetPlaylistEpisodesResponse([], probe));
        }

        public Task<IList<EpisodeModel>?> GetEpisodes(
            Podcast podcast,
            IndexingContext indexingContext,
            IEnumerable<string> knownIds) =>
            throw new NotImplementedException();

        public Task<EpisodeModel> GetEpisodeAsync(
            PlaylistItemSnippet playlistItemSnippet,
            Google.Apis.YouTube.v3.Data.Video videoDetails) =>
            throw new NotImplementedException();

        public Task<EpisodeModel> GetEpisodeAsync(
            SearchResult searchResult,
            Google.Apis.YouTube.v3.Data.Video videoDetails) =>
            throw new NotImplementedException();
    }
}
