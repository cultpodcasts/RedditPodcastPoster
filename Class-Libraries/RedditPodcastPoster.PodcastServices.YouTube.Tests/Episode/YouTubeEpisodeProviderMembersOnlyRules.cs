using System.Xml;
using FluentAssertions;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.Adapters.Inputs;
using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Episodes.Factories;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelVideos;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Episode;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using RedditPodcastPoster.PodcastServices.YouTube.Video;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Episode;

/// <summary>
/// Members-only skip: YouTube channel-membership-gated videos stay publicly listed with real
/// snippet/contentDetails, so they pass the duration and live/upcoming gates. The Data API omits
/// <c>statistics.viewCount</c> for them (while still returning likeCount/commentCount), which is
/// the signal used to skip episode creation. A zero view count is a normal new public upload and
/// must NOT be skipped.
/// </summary>
public class YouTubeEpisodeProviderMembersOnlyRules
{
    private const string ChannelId = "UC_fictional_channel_0000";
    private const string PublicVideoId = "public_vid_01";
    private const string MembersOnlyVideoId = "members_vid_02";

    private readonly AutoMocker _mocker = new();

    private IYouTubeEpisodeProvider Sut => _mocker.CreateInstance<YouTubeEpisodeProvider>();

    [Fact(DisplayName =
        "When a channel's uploads contain a members-only video (statistics.viewCount absent), " +
        "no episode is created for it while the public video still produces an episode.")]
    public async Task MembersOnly_video_is_skipped_and_public_video_is_created()
    {
        // Arrange
        var podcast = new Podcast { Name = "Fictional Preacher Boys Show", YouTubeChannelId = ChannelId };
        var indexingContext = new IndexingContext();

        _mocker.GetMock<IYouTubeChannelVideoRetrievalPolicy>()
            .Setup(x => x.GetUploadsPlaylistReason(It.IsAny<Podcast>()))
            .Returns("test forces uploads-playlist path");

        var playlistItems = new List<PlaylistItem>
        {
            CreatePlaylistItem(PublicVideoId, "Fictional Public Episode About Nothing"),
            CreatePlaylistItem(MembersOnlyVideoId, "Fictional Members-Only Bonus Episode")
        };
        _mocker.GetMock<IYouTubeChannelVideosService>()
            .Setup(x => x.GetChannelVideos(It.IsAny<YouTubeChannelId>(), It.IsAny<IndexingContext>(), It.IsAny<bool>()))
            .ReturnsAsync(new RedditPodcastPoster.PodcastServices.YouTube.Models.ChannelVideos(
                new Google.Apis.YouTube.v3.Data.Channel(), playlistItems));

        _mocker.GetMock<IYouTubeVideoService>()
            .Setup(x => x.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .ReturnsAsync(new List<Google.Apis.YouTube.v3.Data.Video>
            {
                CreatePublicVideo(PublicVideoId),
                CreateMembersOnlyVideo(MembersOnlyVideoId)
            });

        _mocker.GetMock<IEpisodeFromCandidateFactory>()
            .Setup(x => x.Create(It.IsAny<EpisodeCandidate>(), It.IsAny<bool>()))
            .Returns(new RedditPodcastPoster.Models.Episode());

        // Act
        var episodes = await Sut.GetEpisodes(podcast, indexingContext, knownIds: []);

        // Assert
        episodes.Should().NotBeNull();
        episodes!.Should().HaveCount(1, "the members-only video must be skipped and only the public video mapped");

        var adapter = _mocker.GetMock<IEpisodeCatalogueAdapter<YouTubeCatalogueInput>>();
        adapter.Verify(
            x => x.Adapt(It.Is<YouTubeCatalogueInput>(i => i.YouTubeId == PublicVideoId)),
            Times.Once,
            "the public video should be adapted into a candidate");
        adapter.Verify(
            x => x.Adapt(It.Is<YouTubeCatalogueInput>(i => i.YouTubeId == MembersOnlyVideoId)),
            Times.Never,
            "the members-only video must never reach episode creation");

        VerifyWarningLoggedContaining(MembersOnlyVideoId);
    }

    [Fact(DisplayName =
        "When a channel's uploads contain a public video with zero views (viewCount == 0), " +
        "it is still created because zero views is not the members-only signal.")]
    public async Task ZeroView_public_video_is_not_skipped()
    {
        // Arrange
        var podcast = new Podcast { Name = "Fictional Preacher Boys Show", YouTubeChannelId = ChannelId };
        var indexingContext = new IndexingContext();
        const string zeroViewVideoId = "brand_new_vid_03";

        _mocker.GetMock<IYouTubeChannelVideoRetrievalPolicy>()
            .Setup(x => x.GetUploadsPlaylistReason(It.IsAny<Podcast>()))
            .Returns("test forces uploads-playlist path");

        _mocker.GetMock<IYouTubeChannelVideosService>()
            .Setup(x => x.GetChannelVideos(It.IsAny<YouTubeChannelId>(), It.IsAny<IndexingContext>(), It.IsAny<bool>()))
            .ReturnsAsync(new RedditPodcastPoster.PodcastServices.YouTube.Models.ChannelVideos(
                new Google.Apis.YouTube.v3.Data.Channel(), new List<PlaylistItem>
                {
                    CreatePlaylistItem(zeroViewVideoId, "Fictional Brand New Upload")
                }));

        _mocker.GetMock<IYouTubeVideoService>()
            .Setup(x => x.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .ReturnsAsync(new List<Google.Apis.YouTube.v3.Data.Video>
            {
                CreateZeroViewPublicVideo(zeroViewVideoId)
            });

        _mocker.GetMock<IEpisodeFromCandidateFactory>()
            .Setup(x => x.Create(It.IsAny<EpisodeCandidate>(), It.IsAny<bool>()))
            .Returns(new RedditPodcastPoster.Models.Episode());

        // Act
        var episodes = await Sut.GetEpisodes(podcast, indexingContext, knownIds: []);

        // Assert
        episodes.Should().NotBeNull();
        episodes!.Should().HaveCount(1, "a zero-view public upload is not members-only and must be created");
    }

    private void VerifyWarningLoggedContaining(string fragment) =>
        _mocker.GetMock<ILogger<YouTubeEpisodeProvider>>().Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains(fragment)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

    private static PlaylistItem CreatePlaylistItem(string videoId, string title) =>
        new()
        {
            Snippet = new PlaylistItemSnippet
            {
                Title = title,
                ResourceId = new ResourceId { VideoId = videoId },
                PublishedAtDateTimeOffset = DateTimeOffset.UtcNow.AddDays(-1)
            }
        };

    private static Google.Apis.YouTube.v3.Data.Video CreatePublicVideo(string videoId) =>
        BuildVideo(videoId, new VideoStatistics { ViewCount = 5000, LikeCount = 10, CommentCount = 2 });

    private static Google.Apis.YouTube.v3.Data.Video CreateZeroViewPublicVideo(string videoId) =>
        BuildVideo(videoId, new VideoStatistics { ViewCount = 0, LikeCount = 0, CommentCount = 0 });

    private static Google.Apis.YouTube.v3.Data.Video CreateMembersOnlyVideo(string videoId) =>
        // No ViewCount -> members-only signal, other statistics still present.
        BuildVideo(videoId, new VideoStatistics { LikeCount = 1, FavoriteCount = 0, CommentCount = 0 });

    private static Google.Apis.YouTube.v3.Data.Video BuildVideo(string videoId, VideoStatistics statistics) =>
        new()
        {
            Id = videoId,
            Snippet = new VideoSnippet
            {
                Title = $"snippet-{videoId}",
                Description = $"description-{videoId}",
                ChannelId = ChannelId,
                LiveBroadcastContent = "none"
            },
            ContentDetails = new VideoContentDetails
            {
                Duration = XmlConvert.ToString(TimeSpan.FromMinutes(30)),
                ContentRating = new ContentRating()
            },
            Statistics = statistics
        };
}
