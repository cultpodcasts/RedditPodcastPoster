using System.Net;
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
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Channel;

public class YouTubeChannelServiceRules
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public YouTubeChannelServiceRules()
    {
        _mocker.Use<Microsoft.Extensions.Logging.ILogger<YouTubeChannelService>>(NullLogger<YouTubeChannelService>.Instance);
    }

    [Fact(DisplayName = "When YouTube API throws a non-quota exception, GetChannelLookup reports ApiError and does NOT set SkipYouTubeUrlResolving")]
    public async Task When_YouTube_Api_Throws_NonQuota_Exception_Does_Not_Set_Skip_Flag()
    {
        // Arrange
        var mockWrapper = _mocker.GetMock<IYouTubeServiceWrapper>();
        mockWrapper.SetupGet(x => x.YouTubeService).Throws(new Exception(_fixture.Create<string>()));

        var indexingContext = new IndexingContext();
        var channelId = _fixture.Create<YouTubeChannelId>();
        var sut = _mocker.CreateInstance<YouTubeChannelService>();

        // Act
        var result = await sut.GetChannelLookup(channelId, indexingContext);

        // Assert
        result.Channel.Should().BeNull();
        result.Outcome.Should().Be(YouTubeChannelLookupOutcome.ApiError);
        indexingContext.SkipYouTubeUrlResolving.Should().BeFalse();
    }

    [Fact(DisplayName = "When SkipYouTubeUrlResolving is set and nothing is cached, GetChannelLookup reports Skipped without touching the API")]
    public async Task When_Skip_Set_Reports_Skipped()
    {
        // Arrange
        var mockWrapper = _mocker.GetMock<IYouTubeServiceWrapper>();
        var sut = _mocker.CreateInstance<YouTubeChannelService>();

        // Act
        var result = await sut.GetChannelLookup(_fixture.Create<YouTubeChannelId>(),
            new IndexingContext(SkipYouTubeUrlResolving: true));

        // Assert
        result.Outcome.Should().Be(YouTubeChannelLookupOutcome.Skipped);
        mockWrapper.VerifyGet(x => x.YouTubeService, Times.Never);
    }

    [Fact(DisplayName = "When channels.list returns no items, GetChannelLookup reports NotFound without setting the skip flag or touching quota")]
    public async Task When_Api_Returns_No_Items_Reports_NotFound()
    {
        // Arrange
        var handler = new StubHttpMessageHandler("""{ "kind": "youtube#channelListResponse", "items": [] }""");
        var indexingContext = new IndexingContext();
        var sut = CreateSut(handler);

        // Act
        var result = await sut.GetChannelLookup(_fixture.Create<YouTubeChannelId>(), indexingContext);

        // Assert
        result.Outcome.Should().Be(YouTubeChannelLookupOutcome.NotFound);
        result.Channel.Should().BeNull();
        indexingContext.SkipYouTubeUrlResolving.Should().BeFalse();
        indexingContext.YouTubeQuotaExhausted.Should().BeFalse();
        _mocker.GetMock<IYouTubeQuotaUsageTracker>()
            .Verify(x => x.RecordQuotaHitAsync(It.IsAny<Application>(), It.IsAny<ApplicationUsage>(),
                It.IsAny<YouTubeQuotaOperation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "When channels.list returns one item, GetChannelLookup reports Found, requests contentDetails once and caches the channel")]
    public async Task When_Api_Returns_One_Item_Reports_Found_And_Caches()
    {
        // Arrange
        var channelId = _fixture.Create<YouTubeChannelId>();
        var uploadsPlaylistId = _fixture.Create<string>();
        var handler = new StubHttpMessageHandler(
            $$"""{ "items": [ { "id": "{{channelId.ChannelId}}", "contentDetails": { "relatedPlaylists": { "uploads": "{{uploadsPlaylistId}}" } } } ] }""");
        var sut = CreateSut(handler);

        // Act
        var first = await sut.GetChannelLookup(channelId, new IndexingContext(), withContentDetails: true);
        var second = await sut.GetChannelLookup(channelId, new IndexingContext(), withContentDetails: true);

        // Assert
        first.Outcome.Should().Be(YouTubeChannelLookupOutcome.Found);
        first.Channel!.ContentDetails.RelatedPlaylists.Uploads.Should().Be(uploadsPlaylistId);
        second.Outcome.Should().Be(YouTubeChannelLookupOutcome.Found);
        handler.RequestUris.Should().ContainSingle();
        var query = System.Web.HttpUtility.ParseQueryString(handler.RequestUris[0].Query);
        query["part"].Should().Be("contentDetails");
    }

    [Fact(DisplayName = "When a channel was cached earlier in the pass and YouTube is then bypassed, GetChannelLookup still reports Found")]
    public async Task When_Cached_And_Skip_Set_Reports_Found()
    {
        // Arrange
        var channelId = _fixture.Create<YouTubeChannelId>();
        var handler = new StubHttpMessageHandler(
            $$"""{ "items": [ { "id": "{{channelId.ChannelId}}", "contentDetails": { "relatedPlaylists": { "uploads": "{{_fixture.Create<string>()}}" } } } ] }""");
        var sut = CreateSut(handler);
        await sut.GetChannelLookup(channelId, new IndexingContext());

        // Act
        var result = await sut.GetChannelLookup(channelId, new IndexingContext(SkipYouTubeUrlResolving: true));

        // Assert
        result.Outcome.Should().Be(YouTubeChannelLookupOutcome.Found);
        result.Channel.Should().NotBeNull();
        handler.RequestUris.Should().ContainSingle();
    }

    private YouTubeChannelService CreateSut(StubHttpMessageHandler handler)
    {
        var youTubeService = new YouTubeService(new BaseClientService.Initializer
        {
            HttpClientFactory = new StubHttpClientFactory(handler),
            ApiKey = _fixture.Create<string>()
        });
        _mocker.GetMock<IYouTubeServiceWrapper>().SetupGet(x => x.YouTubeService).Returns(youTubeService);
        return _mocker.CreateInstance<YouTubeChannelService>();
    }

    private sealed class StubHttpMessageHandler(string json) : HttpMessageHandler
    {
        public List<Uri> RequestUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : Google.Apis.Http.IHttpClientFactory
    {
        public Google.Apis.Http.ConfigurableHttpClient CreateHttpClient(Google.Apis.Http.CreateHttpClientArgs args) =>
            new(new Google.Apis.Http.ConfigurableMessageHandler(handler));
    }
}