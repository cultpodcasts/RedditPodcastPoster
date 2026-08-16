using AutoFixture;
using FluentAssertions;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using System.Net;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Video;

public class YouTubeVideoServiceRules
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public YouTubeVideoServiceRules()
    {
        _mocker.Use<Microsoft.Extensions.Logging.ILogger<YouTubeVideoService>>(NullLogger<YouTubeVideoService>.Instance);
    }

    [Fact(DisplayName = "When YouTube API throws a non-quota exception, GetVideoContentDetails returns null and does NOT set SkipYouTubeUrlResolving")]
    public async Task When_YouTube_Api_Throws_NonQuota_Exception_Does_Not_Set_Skip_Flag()
    {
        // Arrange
        var mockWrapper = _mocker.GetMock<IYouTubeServiceWrapper>();
        mockWrapper.SetupGet(x => x.YouTubeService).Throws(new Exception(_fixture.Create<string>()));

        var indexingContext = new IndexingContext();
        var videoIds = _fixture.Create<string[]>();
        var sut = _mocker.CreateInstance<YouTubeVideoService>();

        // Act
        var result = await sut.GetVideoContentDetails(mockWrapper.Object, videoIds, indexingContext);

        // Assert
        result.Should().BeNull();
        indexingContext.SkipYouTubeUrlResolving.Should().BeFalse();
    }

    [Fact(DisplayName = "When YouTube API throws a quota exception, GetVideoContentDetails throws YouTubeQuotaException and does NOT set SkipYouTubeUrlResolving")]
    public async Task When_YouTube_Api_Throws_Quota_Exception_Throws_YouTubeQuotaException()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("{ \"error\": { \"code\": 403, \"message\": \"The request cannot be completed because you have exceeded your quota.\" } }")
        };
        var handler = new FakeHttpMessageHandler(response);
        var youtubeService = new YouTubeService(new BaseClientService.Initializer
        {
            HttpClientFactory = new FakeHttpClientFactory(handler),
            ApiKey = _fixture.Create<string>()
        });

        var mockWrapper = _mocker.GetMock<IYouTubeServiceWrapper>();
        mockWrapper.SetupGet(x => x.YouTubeService).Returns(youtubeService);

        var indexingContext = new IndexingContext();
        var videoIds = _fixture.Create<string[]>();
        var sut = _mocker.CreateInstance<YouTubeVideoService>();

        // Act
        var act = () => sut.GetVideoContentDetails(mockWrapper.Object, videoIds, indexingContext);

        // Assert
        await act.Should().ThrowAsync<YouTubeQuotaException>();
        indexingContext.SkipYouTubeUrlResolving.Should().BeFalse();
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
