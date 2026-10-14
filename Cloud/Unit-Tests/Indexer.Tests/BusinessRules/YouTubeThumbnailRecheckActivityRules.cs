using FluentAssertions;
using Indexer.Activities;
using Xunit;

namespace Indexer.Tests.BusinessRules;

public class YouTubeThumbnailRecheckActivityRules
{
    [Theory(DisplayName =
        "The YouTube thumbnail re-check only re-resolves when this run's index passes resolved YouTube without error, " +
        "because replacing an image costs a YouTube API call.")]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(null, null, true)]
    public void youtube_availability_gates_the_recheck(bool? skipYouTube, bool? youTubeError, bool expectedSkip)
    {
        // Arrange
        var indexerContext = new IndexerContext(SkipYouTubeUrlResolving: skipYouTube, YouTubeError: youTubeError);

        // Act
        var indexingContext = YouTubeThumbnailRecheck.ToIndexingContext(indexerContext);

        // Assert
        indexingContext.SkipYouTubeUrlResolving.Should().Be(expectedSkip);
    }
}
