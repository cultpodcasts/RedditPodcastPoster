using FluentAssertions;
using Google.Apis.YouTube.v3.Data;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Channel;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using EpisodeModel = RedditPodcastPoster.Models.Episodes.Episode;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Services;

/// <summary>
/// A submitted URL's list= playlist becomes a new podcast's youTubePlaylistId only when the video's channel owns it;
/// otherwise the new podcast would pair its channel with a playlist the indexer cannot use.
/// </summary>
public class YouTubeUrlCategoriserPlaylistOwnershipRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    private (Uri Url, string ChannelId, string PlaylistId) Arrange(Func<string, string> playlistOwner)
    {
        var channelId = _fixture.CreateYouTubeChannelId();
        var playlistId = _fixture.CreateYouTubePlaylistId();
        var videoId = _fixture.CreateYouTubeId();
        var video = new Google.Apis.YouTube.v3.Data.Video
        {
            Id = videoId,
            Snippet = new VideoSnippet
            {
                ChannelId = channelId,
                ChannelTitle = "Synthetic Channel",
                Title = "Synthetic Episode",
                Description = "Synthetic description",
                PublishedAtDateTimeOffset = DateTimeOffset.UtcNow.AddDays(-1)
            },
            ContentDetails = new VideoContentDetails { Duration = "PT30M", ContentRating = new ContentRating() }
        };
        _mocker.GetMock<ITolerantYouTubeVideoService>()
            .Setup(s => s.GetVideoContentDetails(It.IsAny<IYouTubeServiceWrapper>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexingContext>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<Google.Apis.YouTube.v3.Data.Video> { video });
        _mocker.GetMock<ITolerantYouTubeChannelService>()
            .Setup(s => s.GetChannel(It.IsAny<YouTubeChannelId>(), It.IsAny<IndexingContext>(), It.IsAny<bool>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(new Google.Apis.YouTube.v3.Data.Channel
            {
                Snippet = new ChannelSnippet { Description = "Synthetic channel description" },
                ContentOwnerDetails = new ChannelContentOwnerDetails()
            });
        _mocker.GetMock<ITolerantYouTubePlaylistService>()
            .Setup(s => s.GetPlaylistInfo(It.IsAny<YouTubePlaylistId>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync(new GetPlaylistInfoResponse("Synthetic Playlist", "Synthetic playlist description",
                playlistOwner(channelId), "Owner"));
        return (new Uri($"https://www.youtube.com/watch?v={videoId}&list={playlistId}"), channelId, playlistId);
    }

    [Fact(DisplayName = "A URL playlist owned by the video's channel is kept for the new podcast.")]
    public async Task own_channel_playlist_kept()
    {
        // Arrange
        var (url, _, playlistId) = Arrange(channelId => channelId);

        // Act
        var result = await _mocker.CreateInstance<YouTubeUrlCategoriser>()
            .Resolve(null, new List<EpisodeModel>(), url, new IndexingContext());

        // Assert
        result!.PlaylistId.Should().Be(playlistId);
        result.ShowName.Should().Be("Synthetic Playlist");
    }

    [Fact(DisplayName =
        "A URL playlist owned by another channel is dropped (with the channel's own name and description), so the new podcast is not saved with a mismatched pair.")]
    public async Task other_channel_playlist_dropped()
    {
        // Arrange
        var (url, channelId, _) = Arrange(_ => _fixture.CreateYouTubeChannelId());

        // Act
        var result = await _mocker.CreateInstance<YouTubeUrlCategoriser>()
            .Resolve(null, new List<EpisodeModel>(), url, new IndexingContext());

        // Assert
        result!.PlaylistId.Should().BeNull();
        result.ShowId.Should().Be(channelId);
        result.ShowName.Should().Be("Synthetic Channel");
    }

    [Theory(DisplayName =
        "A URL playlist that cannot be resolved (private, deleted, a WL/LL/RD mix, or a failed lookup) is dropped and the video still resolves, rather than failing the whole submit.")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task unresolvable_playlist_dropped(bool notFound)
    {
        // Arrange
        var (url, channelId, playlistId) = Arrange(channelId => channelId);
        _mocker.GetMock<ITolerantYouTubePlaylistService>()
            .Setup(s => s.GetPlaylistInfo(It.IsAny<YouTubePlaylistId>(), It.IsAny<IndexingContext>()))
            .ThrowsAsync(notFound
                ? new YouTubePlaylistNotFoundException(playlistId)
                : new InvalidOperationException("Unable to obtain playlist-info"));

        // Act
        var result = await _mocker.CreateInstance<YouTubeUrlCategoriser>()
            .Resolve(null, new List<EpisodeModel>(), url, new IndexingContext());

        // Assert
        result!.PlaylistId.Should().BeNull();
        result.ShowId.Should().Be(channelId);
        result.ShowName.Should().Be("Synthetic Channel");
    }
}
