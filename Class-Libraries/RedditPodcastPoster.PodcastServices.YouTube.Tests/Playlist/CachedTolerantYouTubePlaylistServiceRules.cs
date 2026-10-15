using AutoFixture;
using FluentAssertions;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Playlist;

public class CachedTolerantYouTubePlaylistServiceRules
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public CachedTolerantYouTubePlaylistServiceRules()
    {
        _mocker.Use<Microsoft.Extensions.Logging.ILogger<CachedTolerantYouTubePlaylistService>>(
            NullLogger<CachedTolerantYouTubePlaylistService>.Instance);
    }

    [Fact(DisplayName = "When YouTube is bypassed and nothing is cached, the playlist fetch reports Skipped without calling the API")]
    public async Task Bypassed_Without_Cache_Reports_Skipped()
    {
        // Arrange
        var sut = _mocker.CreateInstance<CachedTolerantYouTubePlaylistService>();

        // Act
        var response = await sut.GetPlaylistVideoSnippets(
            _fixture.Create<YouTubePlaylistId>(), new IndexingContext(SkipYouTubeUrlResolving: true));

        // Assert
        response.Result.Should().BeNull();
        response.Failure.Should().Be(YouTubePlaylistFetchFailure.Skipped);
        _mocker.GetMock<ITolerantYouTubePlaylistService>().VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "When a playlist was cached earlier in the pass and YouTube is bypassed, the cached items are returned")]
    public async Task Bypassed_With_Cache_Returns_Cached_Items()
    {
        // Arrange
        var playlistId = _fixture.Create<YouTubePlaylistId>();
        IList<PlaylistItem> items = [new PlaylistItem { Id = _fixture.Create<string>() }];
        _mocker.GetMock<ITolerantYouTubePlaylistService>()
            .Setup(x => x.GetPlaylistVideoSnippets(
                playlistId,
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<PlaylistOrder?>()))
            .ReturnsAsync(new GetPlaylistVideoSnippetsResponse(items));
        var sut = _mocker.CreateInstance<CachedTolerantYouTubePlaylistService>();
        await sut.GetPlaylistVideoSnippets(playlistId, new IndexingContext());

        // Act
        var response = await sut.GetPlaylistVideoSnippets(playlistId, new IndexingContext(SkipYouTubeUrlResolving: true));

        // Assert
        response.Result.Should().BeSameAs(items);
        response.Failure.Should().BeNull();
    }
}