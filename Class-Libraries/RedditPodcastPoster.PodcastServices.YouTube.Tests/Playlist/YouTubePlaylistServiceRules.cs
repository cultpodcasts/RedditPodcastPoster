using FluentAssertions;
using Google;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using System.Net;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Playlist;

public class YouTubePlaylistServiceRules
{
    [Fact(DisplayName = "When YouTube API throws a non-quota exception, GetPlaylistVideoSnippets returns ApiError and does NOT set SkipYouTubeUrlResolving")]
    public async Task When_YouTube_Api_Throws_NonQuota_Exception_Does_Not_Set_Skip_Flag()
    {
        // Arrange
        var quotaTracker = new Mock<IYouTubeQuotaUsageTracker>();
        var sut = new YouTubePlaylistService(quotaTracker.Object, NullLogger<YouTubePlaylistService>.Instance);
        
        var mockWrapper = new Mock<IYouTubeServiceWrapper>();
        // Accessing YouTubeService will throw, triggering the catch(Exception) block
        mockWrapper.SetupGet(x => x.YouTubeService).Throws(new Exception("Simulated API failure"));

        var indexingContext = new IndexingContext();
        var playlistId = new YouTubePlaylistId("playlist-id");

        // Act
        var result = await sut.GetPlaylistVideoSnippets(mockWrapper.Object, playlistId, indexingContext);

        // Assert
        result.Result.Should().BeNull();
        result.Failure.Should().Be(YouTubePlaylistFetchFailure.ApiError);
        indexingContext.SkipYouTubeUrlResolving.Should().BeFalse("non-quota errors should not trigger the kill-switch for the entire run");
    }

    [Fact(DisplayName = "When YouTube API reports NotFound, GetPlaylistVideoSnippets returns NotFound failure and does NOT set SkipYouTubeUrlResolving")]
    public async Task When_YouTube_Api_Reports_NotFound_Does_Not_Set_Skip_Flag()
    {
        // Arrange
        var quotaTracker = new Mock<IYouTubeQuotaUsageTracker>();
        var sut = new YouTubePlaylistService(quotaTracker.Object, NullLogger<YouTubePlaylistService>.Instance);

        var response = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("{ \"error\": { \"code\": 404, \"message\": \"Not Found\" } }")
        };
        var handler = new FakeHttpMessageHandler(response);
        var youtubeService = new YouTubeService(new BaseClientService.Initializer
        {
            HttpClientFactory = new FakeHttpClientFactory(handler),
            ApiKey = "test-key"
        });

        var mockWrapper = new Mock<IYouTubeServiceWrapper>();
        mockWrapper.SetupGet(x => x.YouTubeService).Returns(youtubeService);

        var indexingContext = new IndexingContext();
        var playlistId = new YouTubePlaylistId("playlist-id");

        // Act
        var result = await sut.GetPlaylistVideoSnippets(mockWrapper.Object, playlistId, indexingContext);

        // Assert
        result.Result.Should().BeNull();
        result.Failure.Should().Be(YouTubePlaylistFetchFailure.NotFound);
        indexingContext.SkipYouTubeUrlResolving.Should().BeFalse("playlist not-found should not trigger the kill-switch for the entire run");
    }

    [Fact(DisplayName = "When YouTube API throws a quota exception, GetPlaylistVideoSnippets throws YouTubeQuotaException and does NOT set SkipYouTubeUrlResolving")]
    public async Task When_YouTube_Api_Throws_Quota_Exception_Throws_YouTubeQuotaException()
    {
        // Arrange
        var quotaTracker = new Mock<IYouTubeQuotaUsageTracker>();
        var sut = new YouTubePlaylistService(quotaTracker.Object, NullLogger<YouTubePlaylistService>.Instance);

        var response = new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("{ \"error\": { \"code\": 403, \"message\": \"The request cannot be completed because you have exceeded your quota.\" } }")
        };
        var handler = new FakeHttpMessageHandler(response);
        var youtubeService = new YouTubeService(new BaseClientService.Initializer
        {
            HttpClientFactory = new FakeHttpClientFactory(handler),
            ApiKey = "test-key"
        });

        var mockWrapper = new Mock<IYouTubeServiceWrapper>();
        mockWrapper.SetupGet(x => x.YouTubeService).Returns(youtubeService);

        var indexingContext = new IndexingContext();
        var playlistId = new YouTubePlaylistId("playlist-id");

        // Act
        var act = () => sut.GetPlaylistVideoSnippets(mockWrapper.Object, playlistId, indexingContext);

        // Assert
        await act.Should().ThrowAsync<YouTubeQuotaException>();
        indexingContext.SkipYouTubeUrlResolving.Should().BeFalse("quota errors should not trigger the kill-switch for the entire run anymore");
    }

    private sealed class FakeHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(response);
        }
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : Google.Apis.Http.IHttpClientFactory
    {
        public Google.Apis.Http.ConfigurableHttpClient CreateHttpClient(Google.Apis.Http.CreateHttpClientArgs args)
        {
            return new Google.Apis.Http.ConfigurableHttpClient(new Google.Apis.Http.ConfigurableMessageHandler(handler));
        }
    }
}
