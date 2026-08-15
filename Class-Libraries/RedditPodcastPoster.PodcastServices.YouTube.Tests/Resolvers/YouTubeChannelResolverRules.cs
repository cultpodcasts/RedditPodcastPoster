using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using System.Net;
using Google.Apis.YouTube.v3;
using Google.Apis.Services;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Resolvers;

public class YouTubeChannelResolverRules
{
    [Fact(DisplayName = "When YouTube API throws an exception during channel search, FindChannelsSnippets returns null and does NOT set SkipYouTubeUrlResolving")]
    public async Task When_YouTube_Api_Throws_Exception_During_Search_Does_Not_Set_Skip_Flag()
    {
        // Arrange
        var mockWrapper = new Mock<IYouTubeServiceWrapper>();
        // Accessing YouTubeService will throw
        mockWrapper.SetupGet(x => x.YouTubeService).Throws(new Exception("Simulated API failure"));

        var sut = new YouTubeChannelResolver(mockWrapper.Object, NullLogger<YouTubeChannelResolver>.Instance);
        
        var indexingContext = new IndexingContext();

        // Act
        var result = await sut.FindChannelsSnippets("channel-name", "video-title", indexingContext);

        // Assert
        result.Should().BeNull();
        indexingContext.SkipYouTubeUrlResolving.Should().BeFalse("non-quota errors should not trigger the kill-switch for the entire run");
    }

    [Fact(DisplayName = "When YouTube API throws a quota exception during channel search, FindChannelsSnippets throws YouTubeQuotaException and does NOT set SkipYouTubeUrlResolving")]
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

        var sut = new YouTubeChannelResolver(mockWrapper.Object, NullLogger<YouTubeChannelResolver>.Instance);
        
        var indexingContext = new IndexingContext();

        // Act
        var act = () => sut.FindChannelsSnippets("channel-name", "video-title", indexingContext);

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
