using FluentAssertions;
using RedditPodcastPoster.PodcastServices.YouTube.Services;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Services;

public class YouTubeVideoDurationMatcherTests
{
    [Fact]
    public void IsAcceptableDurationMatch_WithAnyEpisode_RejectsMissingVideoDuration()
    {
        YouTubeVideoDurationMatcher.IsAcceptableDurationMatch(TimeSpan.FromMinutes(3), null).Should().BeFalse();
    }

    [Fact]
    public void IsAcceptableDurationMatch_WithShortEpisode_AcceptsMatchingVideoDuration()
    {
        YouTubeVideoDurationMatcher
            .IsAcceptableDurationMatch(TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(3))
            .Should()
            .BeTrue();
    }

    [Fact]
    public void IsAcceptableDurationMatch_WithLongFormEpisode_RejectsMissingVideoDuration()
    {
        YouTubeVideoDurationMatcher.IsAcceptableDurationMatch(TimeSpan.FromMinutes(60), null).Should().BeFalse();
    }

    [Fact]
    public void IsAcceptableDurationMatch_WithLongFormEpisode_RejectsVideoMoreThanFivePercentShorter()
    {
        var episodeLength = TimeSpan.FromMinutes(60);
        var videoLength = TimeSpan.FromMinutes(56);

        YouTubeVideoDurationMatcher.IsAcceptableDurationMatch(episodeLength, videoLength).Should().BeFalse();
    }

    [Fact]
    public void IsAcceptableDurationMatch_WithLongFormEpisode_AcceptsVideoWithinFivePercent()
    {
        var episodeLength = TimeSpan.FromMinutes(60);
        var videoLength = TimeSpan.FromMinutes(57);

        YouTubeVideoDurationMatcher.IsAcceptableDurationMatch(episodeLength, videoLength).Should().BeTrue();
    }
}
