using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
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
}
