using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Episode;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Episode;

/// <summary>
/// Playlist API failures must surface as Failure on GetPlaylistEpisodesResponse so the retrieval
/// handler can name the podcast and ask operators to refresh YouTubePlaylistId.
/// </summary>
public class YouTubeEpisodeProviderPlaylistFetchFailureRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    private IYouTubeEpisodeProvider Sut => _mocker.CreateInstance<YouTubeEpisodeProvider>();

    [Fact(DisplayName =
        "When playlistItems.list reports NotFound, GetPlaylistEpisodes returns Failure=NotFound " +
        "because the podcast's stored playlist id is gone and must be replaced.")]
    public async Task Propagates_playlist_not_found_failure()
    {
        // Arrange
        var playlistId = _fixture.CreateYouTubePlaylistId();
        var podcast = _fixture.CreatePodcast();
        var indexingContext = new IndexingContext(DomainTestFixture.UtcDaysAgo(2));
        _mocker.GetMock<ITolerantYouTubePlaylistService>()
            .Setup(x => x.GetPlaylistVideoSnippets(
                It.IsAny<YouTubePlaylistId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<PlaylistOrder?>()))
            .ReturnsAsync(new GetPlaylistVideoSnippetsResponse(
                null,
                Failure: YouTubePlaylistFetchFailure.NotFound));

        // Act
        var response = await Sut.GetPlaylistEpisodes(
            podcast,
            new YouTubePlaylistId(playlistId),
            new YouTubeChannelId(_fixture.CreateYouTubeChannelId()),
            indexingContext);

        // Assert
        response.Results.Should().BeNull();
        response.Failure.Should().Be(YouTubePlaylistFetchFailure.NotFound);
    }
}
