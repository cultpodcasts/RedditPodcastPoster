using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;
using Xunit;
using static RedditPodcastPoster.Episodes.TestSupport.Fixtures.YouTubeThumbnailSpecimens;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Thumbnails;

public class YouTubeThumbnailUrlsTests
{
    private readonly DomainTestFixture _fixture = new();

    [Theory(DisplayName =
        "Only maxresdefault and hq720 are withdrawable high-resolution tiers worth re-checking after release.")]
    [InlineData(MaxresTier, true)]
    [InlineData(Hq720Tier, true)]
    [InlineData(StandardTier, false)]
    [InlineData(HighTier, false)]
    [InlineData(MediumTier, false)]
    [InlineData(DefaultTier, false)]
    public void withdrawable_tiers(string tier, bool expected)
    {
        // Arrange
        var url = Url(_fixture.CreateYouTubeId(), tier);

        // Act
        var withdrawable = YouTubeThumbnailUrls.IsWithdrawableHighResolution(url);

        // Assert
        withdrawable.Should().Be(expected);
    }

    [Theory(DisplayName = "Only default.jpg is the default tier.")]
    [InlineData(DefaultTier, true)]
    [InlineData(MaxresTier, false)]
    [InlineData(MediumTier, false)]
    public void default_tier(string tier, bool expected)
    {
        // Arrange
        var url = Url(_fixture.CreateYouTubeId(), tier);

        // Act
        var isDefault = YouTubeThumbnailUrls.IsDefaultTier(url);

        // Assert
        isDefault.Should().Be(expected);
    }
}
