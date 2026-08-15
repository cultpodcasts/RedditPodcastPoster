using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.ChannelSnippets;

public class YouTubeChannelVideoSnippetsServiceRules
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public YouTubeChannelVideoSnippetsServiceRules()
    {
        _mocker.Use<Microsoft.Extensions.Logging.ILogger<YouTubeChannelVideoSnippetsService>>(NullLogger<YouTubeChannelVideoSnippetsService>.Instance);
    }

    [Fact(DisplayName = "When YouTube API throws a non-quota exception, GetLatestChannelVideoSnippets returns partial results and does NOT set SkipYouTubeUrlResolving")]
    public async Task When_YouTube_Api_Throws_NonQuota_Exception_Does_Not_Set_Skip_Flag()
    {
        // Arrange
        var mockWrapper = _mocker.GetMock<IYouTubeServiceWrapper>();
        mockWrapper.SetupGet(x => x.YouTubeService).Throws(new Exception("Simulated API failure"));

        var indexingContext = new IndexingContext();
        var channelId = _fixture.Create<YouTubeChannelId>();
        var sut = _mocker.CreateInstance<YouTubeChannelVideoSnippetsService>();

        // Act
        var result = await sut.GetLatestChannelVideoSnippets(mockWrapper.Object, channelId, indexingContext);

        // Assert
        result.Should().BeEmpty();
        indexingContext.SkipYouTubeUrlResolving.Should().BeFalse();
    }
}
