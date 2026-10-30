using FluentAssertions;
using Google.Apis.YouTube.v3.Data;
using Moq;
using Moq.AutoMock;
using System.Xml;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.Adapters.Inputs;
using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Episodes.Factories;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Episode;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using EpisodeModel = RedditPodcastPoster.Models.Episodes.Episode;
using VideoModel = Google.Apis.YouTube.v3.Data.Video;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Episode;

/// <summary>
/// The build step shared by the full playlist walk and the YouTube indexing hints uploads check. With a playlist, the
/// playlist alone is the parent's YouTube source: every video in it is built, whichever channel uploaded it.
/// </summary>
public class YouTubeEpisodeProviderBuildPlaylistEpisodesRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly IndexingContext _indexingContext = new(DomainTestFixture.UtcDaysAgo(7));
    private readonly List<VideoModel> _videos = [];

    public YouTubeEpisodeProviderBuildPlaylistEpisodesRules()
    {
        _mocker.GetMock<ITolerantYouTubeVideoService>()
            .Setup(x => x.GetVideoContentDetails(It.IsAny<IYouTubeServiceWrapper>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexingContext>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(() => _videos);
        _mocker.GetMock<IEpisodeFromCandidateFactory>()
            .Setup(x => x.Create(It.IsAny<EpisodeCandidate>(), It.IsAny<bool>()))
            .Returns(() => new EpisodeModel());
    }

    private PlaylistItem Item(string videoId) => new()
    {
        Snippet = new PlaylistItemSnippet
        {
            Title = _fixture.CreateTitle(),
            PublishedAtDateTimeOffset = DomainTestFixture.UtcDaysAgo(1),
            ResourceId = new ResourceId { VideoId = videoId }
        }
    };

    private VideoModel Video(string videoId, string channelId) => new()
    {
        Id = videoId,
        Snippet = new VideoSnippet
        {
            ChannelId = channelId,
            Title = _fixture.CreateTitle(),
            Description = _fixture.CreateTitle(),
            LiveBroadcastContent = "none",
            PublishedAtDateTimeOffset = DomainTestFixture.UtcDaysAgo(1)
        },
        ContentDetails = new VideoContentDetails
        {
            Duration = XmlConvert.ToString(_fixture.CreateDuration()),
            ContentRating = new ContentRating()
        },
        Statistics = new VideoStatistics { ViewCount = 1000, LikeCount = 10, CommentCount = 2 }
    };

    [Fact(DisplayName =
        "Every playlist video is built whichever channel uploaded it, and the parent's channel id plays no part, " +
        "because a playlist is the parent's only YouTube source (Fame Under Fire: a BBC News Entertainment playlist " +
        "on a parent whose channel id was BBC World Service indexed nothing).")]
    public async Task videos_from_any_channel_are_built()
    {
        // Arrange
        var own = _fixture.CreateYouTubeId();
        var other = _fixture.CreateYouTubeId();
        var podcast = _fixture.CreatePodcast();
        podcast.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
        podcast.YouTubePlaylistId = _fixture.CreateYouTubePlaylistId();
        _videos.Add(Video(own, podcast.YouTubeChannelId));
        _videos.Add(Video(other, _fixture.CreateYouTubeChannelId()));
        var sut = _mocker.CreateInstance<YouTubeEpisodeProvider>();

        // Act
        var response = await sut.BuildPlaylistEpisodes(podcast, _indexingContext, [Item(own), Item(other)]);

        // Assert
        response.Results.Should().HaveCount(2);
        _mocker.GetMock<IEpisodeCatalogueAdapter<YouTubeCatalogueInput>>().Verify(
            x => x.Adapt(It.Is<YouTubeCatalogueInput>(i => i.YouTubeId == other)), Times.Once);
    }

    [Fact(DisplayName =
        "A playlist parent with no channel id builds its playlist videos, because the channel id is not needed.")]
    public async Task playlist_without_channel_id_builds()
    {
        // Arrange
        var videoId = _fixture.CreateYouTubeId();
        var podcast = _fixture.CreatePodcast();
        podcast.YouTubeChannelId = string.Empty;
        podcast.YouTubePlaylistId = _fixture.CreateYouTubePlaylistId();
        _videos.Add(Video(videoId, _fixture.CreateYouTubeChannelId()));
        var sut = _mocker.CreateInstance<YouTubeEpisodeProvider>();

        // Act
        var response = await sut.BuildPlaylistEpisodes(podcast, _indexingContext, [Item(videoId)]);

        // Assert
        response.Results.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "A failed videos.list leaves Results null, so the caller does not move the hints past unbuilt uploads.")]
    public async Task failed_lookup_has_null_results()
    {
        // Arrange
        var sut = _mocker.CreateInstance<YouTubeEpisodeProvider>();

        // Act
        var response = await sut.BuildPlaylistEpisodes(_fixture.CreatePodcast(), _indexingContext,
            [Item(_fixture.CreateYouTubeId())]);

        // Assert
        response.Results.Should().BeNull();
    }

    [Fact(DisplayName =
        "A complete playlist walk whose build fails (videos.list returned nothing) is Incomplete, because the hints " +
        "may only move past items that were actually built.")]
    public async Task complete_walk_with_failed_build_is_incomplete()
    {
        // Arrange
        var playlistId = new YouTubePlaylistId(_fixture.CreateYouTubePlaylistId());
        _mocker.GetMock<ITolerantYouTubePlaylistService>()
            .Setup(x => x.GetPlaylistVideoSnippets(playlistId, It.IsAny<IndexingContext>(), It.IsAny<bool>(),
                It.IsAny<bool>(), It.IsAny<PlaylistOrder?>()))
            .ReturnsAsync(new GetPlaylistVideoSnippetsResponse([Item(_fixture.CreateYouTubeId())], TotalResults: 400,
                WalkOutcome: YouTubePlaylistWalkOutcome.Complete));
        var sut = _mocker.CreateInstance<YouTubeEpisodeProvider>();

        // Act
        var response = await sut.GetPlaylistEpisodes(_fixture.CreatePodcast(), playlistId, _indexingContext);

        // Assert
        response.Results.Should().BeNull();
        response.TotalResults.Should().Be(400);
        response.WalkOutcome.Should().Be(YouTubePlaylistWalkOutcome.Incomplete);
    }
}
