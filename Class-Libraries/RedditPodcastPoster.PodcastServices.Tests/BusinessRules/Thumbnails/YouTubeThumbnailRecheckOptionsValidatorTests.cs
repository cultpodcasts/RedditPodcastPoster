using FluentAssertions;
using RedditPodcastPoster.PodcastServices.Thumbnails;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Thumbnails;

public class YouTubeThumbnailRecheckOptionsValidatorTests
{
    private readonly YouTubeThumbnailRecheckOptionsValidator _sut = new();

    [Fact(DisplayName = "Default re-check options are valid, so the re-check runs without app settings.")]
    public void defaults_are_valid()
    {
        // Arrange
        var options = new YouTubeThumbnailRecheckOptions();

        // Act
        var result = _sut.Validate(null, options);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Theory(DisplayName = "A non-positive re-check window or per-run budget fails validation and names its config key.")]
    [InlineData(nameof(YouTubeThumbnailRecheckOptions.RecheckWindowHours))]
    [InlineData(nameof(YouTubeThumbnailRecheckOptions.MaxStoredImageProbesPerRun))]
    [InlineData(nameof(YouTubeThumbnailRecheckOptions.MaxReResolvesPerRun))]
    public void non_positive_values_fail(string property)
    {
        // Arrange
        var options = new YouTubeThumbnailRecheckOptions();
        typeof(YouTubeThumbnailRecheckOptions).GetProperty(property)!.SetValue(options, 0);

        // Act
        var result = _sut.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain($"{YouTubeThumbnailRecheckOptions.SectionName}__{property}");
    }
}
