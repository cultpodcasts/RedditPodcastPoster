using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Channel;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
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

    [Fact(DisplayName = "When YouTube API throws a non-quota exception, GetChannel returns null and does NOT set SkipYouTubeUrlResolving")]
    public async Task When_YouTube_Api_Throws_NonQuota_Exception_Does_Not_Set_Skip_Flag()
    {
        // Arrange
        var mockWrapper = _mocker.GetMock<IYouTubeServiceWrapper>();
        mockWrapper.SetupGet(x => x.YouTubeService).Throws(new Exception(_fixture.Create<string>()));

        var indexingContext = new IndexingContext();
        var channelId = _fixture.Create<YouTubeChannelId>();
        var sut = _mocker.CreateInstance<YouTubeChannelService>();

        // Act
        var result = await sut.GetChannel(channelId, indexingContext);

        // Assert
        result.Should().BeNull();
        indexingContext.SkipYouTubeUrlResolving.Should().BeFalse();
    }
}
