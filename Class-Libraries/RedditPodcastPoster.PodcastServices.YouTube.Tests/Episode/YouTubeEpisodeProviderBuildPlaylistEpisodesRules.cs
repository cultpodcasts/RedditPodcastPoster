using FluentAssertions;
using Google.Apis.YouTube.v3.Data;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Episode;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using VideoModel = Google.Apis.YouTube.v3.Data.Video;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Episode;

/// <summary>
/// The build step shared by the full playlist walk and the YouTube indexing hints uploads check.
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
        Snippet = new VideoSnippet { ChannelId = channelId, Title = _fixture.CreateTitle() }
    };

    [Fact(DisplayName =
        "A video from another channel is filtered out of the build, so an uploads-head member and a full-walk item " +
        "obey the same channel rule.")]
    public async Task other_channel_video_is_filtered_out()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var otherId = _fixture.CreateYouTubeId();
        _videos.Add(Video(otherId, _fixture.CreateYouTubeChannelId()));
        var sut = _mocker.CreateInstance<YouTubeEpisodeProvider>();

        // Act
        var response = await sut.BuildPlaylistEpisodes(_fixture.CreatePodcast(), new YouTubeChannelId(channelId),
            _indexingContext, [Item(otherId)]);

        // Assert
        response.Results.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A failed videos.list leaves Results null, so the caller does not move the hints past unbuilt uploads.")]
    public async Task failed_lookup_has_null_results()
    {
        // Arrange
        var sut = _mocker.CreateInstance<YouTubeEpisodeProvider>();

        // Act
        var response = await sut.BuildPlaylistEpisodes(_fixture.CreatePodcast(),
            new YouTubeChannelId(_fixture.CreateYouTubeChannelId()), _indexingContext, [Item(_fixture.CreateYouTubeId())]);

        // Assert
        response.Results.Should().BeNull();
    }
}
