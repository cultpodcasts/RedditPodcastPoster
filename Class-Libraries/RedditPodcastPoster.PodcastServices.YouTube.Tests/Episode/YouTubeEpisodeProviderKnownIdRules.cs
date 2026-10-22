using FluentAssertions;
using Google.Apis.YouTube.v3.Data;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Episode;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Episode;

/// <summary>
/// Playlist discovery mirrors channel discovery: a video already stored for the podcast is not looked up again
/// (videos.list), its thumbnails are not probed and no episode is rebuilt for it.
/// </summary>
public class YouTubeEpisodeProviderKnownIdRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly IndexingContext _indexingContext = new(DomainTestFixture.UtcDaysAgo(7));
    private readonly string _channelId;
    private readonly List<PlaylistItem> _playlistItems = [];
    private IEnumerable<string>? _requestedVideoIds;

    public YouTubeEpisodeProviderKnownIdRules()
    {
        _channelId = _fixture.CreateYouTubeChannelId();
        _mocker.GetMock<ITolerantYouTubePlaylistService>()
            .Setup(x => x.GetPlaylistVideoSnippets(
                It.IsAny<YouTubePlaylistId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<PlaylistOrder?>(),
                It.IsAny<bool>()))
            .ReturnsAsync(() => new GetPlaylistVideoSnippetsResponse(_playlistItems));
        _mocker.GetMock<ITolerantYouTubeVideoService>()
            .Setup(x => x.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .Callback<IYouTubeServiceWrapper, IEnumerable<string>, IndexingContext?, bool, bool, bool>(
                (_, ids, _, _, _, _) => _requestedVideoIds = ids.ToArray())
            .ReturnsAsync(() => []);
    }

    [Fact(DisplayName =
        "When playlist discovery finds a video the podcast already has, only the new videos are looked up, because " +
        "re-fetching and re-probing known videos is wasted quota and time on every pass.")]
    public async Task known_videos_are_not_looked_up_again()
    {
        // Arrange
        var knownId = _fixture.CreateYouTubeId();
        var newId = _fixture.CreateYouTubeId();
        _playlistItems.Add(InWindowItem(knownId));
        _playlistItems.Add(InWindowItem(newId));
        var sut = _mocker.CreateInstance<YouTubeEpisodeProvider>();

        // Act
        await sut.GetPlaylistEpisodes(
            _fixture.CreatePodcast(),
            new YouTubePlaylistId(_fixture.CreateYouTubePlaylistId()),
            new YouTubeChannelId(_channelId),
            _indexingContext,
            knownIds: [knownId]);

        // Assert
        _requestedVideoIds.Should().BeEquivalentTo([newId]);
    }

    [Fact(DisplayName =
        "When every in-window playlist video is already known, discovery makes no videos.list call and returns no " +
        "episodes, because there is nothing new to build.")]
    public async Task all_known_videos_skip_the_lookup()
    {
        // Arrange
        var knownId = _fixture.CreateYouTubeId();
        _playlistItems.Add(InWindowItem(knownId));
        var sut = _mocker.CreateInstance<YouTubeEpisodeProvider>();

        // Act
        var response = await sut.GetPlaylistEpisodes(
            _fixture.CreatePodcast(),
            new YouTubePlaylistId(_fixture.CreateYouTubePlaylistId()),
            new YouTubeChannelId(_channelId),
            _indexingContext,
            knownIds: [knownId]);

        // Assert
        response.Results.Should().BeEmpty();
        _mocker.GetMock<ITolerantYouTubeVideoService>().Verify(
            x => x.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()),
            Times.Never);
    }

    private PlaylistItem InWindowItem(string videoId)
    {
        var addedAt = new DateTimeOffset(
            DomainTestFixture.UtcAtTime(-1, _fixture.CreateNonMidnightTimeOfDay()), TimeSpan.Zero);
        return new PlaylistItem
        {
            Snippet = new PlaylistItemSnippet
            {
                Title = _fixture.CreateTitle(),
                ResourceId = new ResourceId { VideoId = videoId },
                PublishedAtDateTimeOffset = addedAt
            },
            ContentDetails = new PlaylistItemContentDetails
            {
                VideoId = videoId,
                VideoPublishedAtDateTimeOffset = addedAt
            }
        };
    }
}
