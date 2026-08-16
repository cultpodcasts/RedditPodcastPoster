using System.Xml;
using FluentAssertions;
using Google.Apis.YouTube.v3.Data;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.Adapters.Inputs;
using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Episodes.Factories;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Episode;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using EpisodeModel = RedditPodcastPoster.Models.Episodes.Episode;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Episode;

/// <summary>
/// Creators schedule uploads: the video is added to the show playlist when it is uploaded and only
/// becomes public days later. Playlist discovery must window such items on the video's publication
/// and stamp the episode with it, otherwise release day sees no new episode.
/// </summary>
public class YouTubeEpisodeProviderScheduledUploadRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    private IYouTubeEpisodeProvider Sut => _mocker.CreateInstance<YouTubeEpisodeProvider>();

    [Fact(DisplayName =
        "When a playlist holds a scheduled upload added before the released-since window but published inside it, " +
        "playlist discovery yields the episode dated by the video's publication, because release day is when the " +
        "episode becomes available.")]
    public async Task Scheduled_upload_is_discovered_and_dated_by_video_publication()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var playlistId = _fixture.CreateYouTubePlaylistId();
        var scheduledVideoId = _fixture.CreateYouTubeId();
        var addedToPlaylistAt = DomainTestFixture.UtcAtTime(-5, TimeSpan.FromHours(14));
        var videoPublishedAt = DomainTestFixture.UtcAtTime(0, TimeSpan.FromHours(15));
        var indexingContext = new IndexingContext(DomainTestFixture.UtcDaysAgo(2));

        _mocker.GetMock<ITolerantYouTubePlaylistService>()
            .Setup(x => x.GetPlaylistVideoSnippets(
                It.IsAny<YouTubePlaylistId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<PlaylistOrder?>()))
            .ReturnsAsync(new GetPlaylistVideoSnippetsResponse(
                [CreateScheduledPlaylistItem(scheduledVideoId, addedToPlaylistAt, videoPublishedAt)]));

        _mocker.GetMock<ITolerantYouTubeVideoService>()
            .Setup(x => x.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .ReturnsAsync([CreatePublicVideo(scheduledVideoId, channelId, videoPublishedAt)]);

        _mocker.GetMock<IEpisodeFromCandidateFactory>()
            .Setup(x => x.Create(It.IsAny<EpisodeCandidate>(), It.IsAny<bool>()))
            .Returns(new EpisodeModel());

        // Act
        var podcast = _fixture.CreatePodcast();
        var response = await Sut.GetPlaylistEpisodes(
            podcast,
            new YouTubePlaylistId(playlistId),
            new YouTubeChannelId(channelId),
            indexingContext,
            playlistOrder: PlaylistOrder.Arbitrary);

        // Assert
        response.Results.Should().ContainSingle();
        _mocker.GetMock<IEpisodeCatalogueAdapter<YouTubeCatalogueInput>>().Verify(
            x => x.Adapt(It.Is<YouTubeCatalogueInput>(i =>
                i.YouTubeId == scheduledVideoId && i.Release == videoPublishedAt)),
            Times.Once,
            "the episode must be dated by the video's publication, not by when it joined the playlist");
    }

    [Fact(DisplayName =
        "When a playlist holds a video added and published before the released-since window, playlist discovery " +
        "yields nothing, because widening to video-published-at must not pull genuinely old episodes back in.")]
    public async Task Video_published_before_the_window_is_still_excluded()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var playlistId = _fixture.CreateYouTubePlaylistId();
        var staleVideoId = _fixture.CreateYouTubeId();
        var addedToPlaylistAt = DomainTestFixture.UtcAtTime(-5, TimeSpan.FromHours(14));
        var videoPublishedAt = DomainTestFixture.UtcAtTime(-5, TimeSpan.FromHours(15));
        var indexingContext = new IndexingContext(DomainTestFixture.UtcDaysAgo(2));

        _mocker.GetMock<ITolerantYouTubePlaylistService>()
            .Setup(x => x.GetPlaylistVideoSnippets(
                It.IsAny<YouTubePlaylistId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<PlaylistOrder?>()))
            .ReturnsAsync(new GetPlaylistVideoSnippetsResponse(
                [CreateScheduledPlaylistItem(staleVideoId, addedToPlaylistAt, videoPublishedAt)]));

        _mocker.GetMock<ITolerantYouTubeVideoService>()
            .Setup(x => x.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .ReturnsAsync([CreatePublicVideo(staleVideoId, channelId, videoPublishedAt)]);

        // Act
        var podcast = _fixture.CreatePodcast();
        var response = await Sut.GetPlaylistEpisodes(
            podcast,
            new YouTubePlaylistId(playlistId),
            new YouTubeChannelId(channelId),
            indexingContext,
            playlistOrder: PlaylistOrder.Arbitrary);

        // Assert
        response.Results.Should().BeNullOrEmpty();
        _mocker.GetMock<IEpisodeCatalogueAdapter<YouTubeCatalogueInput>>().Verify(
            x => x.Adapt(It.IsAny<YouTubeCatalogueInput>()),
            Times.Never);
    }

    private PlaylistItem CreateScheduledPlaylistItem(
        string videoId,
        DateTime addedToPlaylistAt,
        DateTime videoPublishedAt) =>
        new()
        {
            Snippet = new PlaylistItemSnippet
            {
                Title = _fixture.CreateTitle(),
                ResourceId = new ResourceId { VideoId = videoId },
                PublishedAtDateTimeOffset = new DateTimeOffset(addedToPlaylistAt, TimeSpan.Zero)
            },
            ContentDetails = new PlaylistItemContentDetails
            {
                VideoId = videoId,
                VideoPublishedAtDateTimeOffset = new DateTimeOffset(videoPublishedAt, TimeSpan.Zero)
            }
        };

    private Google.Apis.YouTube.v3.Data.Video CreatePublicVideo(
        string videoId,
        string channelId,
        DateTime videoPublishedAt) =>
        new()
        {
            Id = videoId,
            Snippet = new VideoSnippet
            {
                Title = _fixture.CreateTitle(),
                Description = _fixture.CreateTitle(),
                ChannelId = channelId,
                LiveBroadcastContent = "none",
                PublishedAtDateTimeOffset = new DateTimeOffset(videoPublishedAt, TimeSpan.Zero)
            },
            ContentDetails = new VideoContentDetails
            {
                Duration = XmlConvert.ToString(_fixture.CreateDuration()),
                ContentRating = new ContentRating()
            },
            Statistics = new VideoStatistics { ViewCount = 1000, LikeCount = 10, CommentCount = 2 }
        };
}
