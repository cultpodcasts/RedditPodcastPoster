using AutoFixture;
using FluentAssertions;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Channel;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using RedditPodcastPoster.PodcastServices.YouTube.Factories;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using System.Net;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Services;

public class YouTubeSearcherRules
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public YouTubeSearcherRules()
    {
        _mocker.Use<Microsoft.Extensions.Logging.ILogger<YouTubeSearcher>>(NullLogger<YouTubeSearcher>.Instance);
    }

    [Fact(DisplayName = "When YouTube API throws an exception during search, Search returns partial results and does NOT set SkipYouTubeUrlResolving")]
    public async Task When_YouTube_Api_Throws_Exception_During_Search_Does_Not_Set_Skip_Flag()
    {
        // Arrange
        var mockWrapper = _mocker.GetMock<IYouTubeServiceWrapper>();
        mockWrapper.SetupGet(x => x.YouTubeService).Throws(new Exception(_fixture.Create<string>()));

        var indexingContext = new IndexingContext();
        var sut = _mocker.CreateInstance<YouTubeSearcher>();

        // Act
        var result = await sut.Search(_fixture.Create<string>(), indexingContext);

        // Assert
        result.Should().BeEmpty();
        indexingContext.SkipYouTubeUrlResolving.Should().BeFalse();
    }

    [Fact(DisplayName = "When YouTube API throws a quota exception during search, Search throws YouTubeQuotaException and does NOT set SkipYouTubeUrlResolving")]
    public async Task When_YouTube_Api_Throws_Quota_Exception_During_Search_Throws_YouTubeQuotaException()
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
        var sut = _mocker.CreateInstance<YouTubeSearcher>();

        // Act
        var act = () => sut.Search(_fixture.Create<string>(), indexingContext);

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
