using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Channel;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Factories;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using System.Net;
using Google.Apis.YouTube.v3;
using Google.Apis.Services;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Services;

public class YouTubeSearcherRules
{
    [Fact(DisplayName = "When YouTube API throws an exception during search, Search returns partial results and does NOT set SkipYouTubeUrlResolving")]
    public async Task When_YouTube_Api_Throws_Exception_During_Search_Does_Not_Set_Skip_Flag()
    {
        // Arrange
        var mockWrapper = new Mock<IYouTubeServiceWrapper>();
        // Accessing YouTubeService will throw
        mockWrapper.SetupGet(x => x.YouTubeService).Throws(new Exception("Simulated API failure"));

        var httpClientFactory = new Mock<INoRedirectHttpClientFactory>();
        var videoService = new Mock<ITolerantYouTubeVideoService>();
        var channelService = new Mock<ITolerantYouTubeChannelService>();
        var thumbnailResolver = new Mock<IYouTubeThumbnailResolver>();

        var sut = new YouTubeSearcher(
            mockWrapper.Object, 
            httpClientFactory.Object, 
            videoService.Object, 
            channelService.Object, 
            thumbnailResolver.Object, 
            NullLogger<YouTubeSearcher>.Instance);
        
        var indexingContext = new IndexingContext();

        // Act
        var result = await sut.Search("query", indexingContext);

        // Assert
        result.Should().BeEmpty();
        indexingContext.SkipYouTubeUrlResolving.Should().BeFalse("non-quota errors should not trigger the kill-switch for the entire run");
    }

    [Fact(DisplayName = "When YouTube API throws a quota exception during search, Search throws YouTubeQuotaException and does NOT set SkipYouTubeUrlResolving")]
    public async Task When_YouTube_Api_Throws_Quota_Exception_During_Search_Throws_YouTubeQuotaException()
    {
        // Arrange
        var mockWrapper = new Mock<IYouTubeServiceWrapper>();
        
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

        mockWrapper.SetupGet(x => x.YouTubeService).Returns(youtubeService);

        var httpClientFactory = new Mock<INoRedirectHttpClientFactory>();
        var videoService = new Mock<ITolerantYouTubeVideoService>();
        var channelService = new Mock<ITolerantYouTubeChannelService>();
        var thumbnailResolver = new Mock<IYouTubeThumbnailResolver>();

        var sut = new YouTubeSearcher(
            mockWrapper.Object, 
            httpClientFactory.Object, 
            videoService.Object, 
            channelService.Object, 
            thumbnailResolver.Object, 
            NullLogger<YouTubeSearcher>.Instance);
        
        var indexingContext = new IndexingContext();

        // Act
        var act = () => sut.Search("query", indexingContext);

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
