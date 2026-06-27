using FluentAssertions;
using RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Thumbnails;

public class YouTubeThumbnailValidationTests
{
    [Theory]
    [InlineData(1280, 720, false, true)]
    [InlineData(640, 480, false, true)]
    [InlineData(120, 90, false, false)]
    [InlineData(120, 90, true, true)]
    public void IsUsableThumbnail_DetectsPlaceholderDimensions(
        int width,
        int height,
        bool isDefaultTier,
        bool expected)
    {
        YouTubeThumbnailValidation.IsUsableThumbnail(width, height, isDefaultTier).Should().Be(expected);
    }
}
