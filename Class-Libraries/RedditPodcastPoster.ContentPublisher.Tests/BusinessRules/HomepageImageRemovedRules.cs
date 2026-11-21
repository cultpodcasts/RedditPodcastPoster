using System.Text.Json;
using FluentAssertions;
using RedditPodcastPoster.ContentPublisher.Models;
using RedditPodcastPoster.ContentPublisher.Publishers;
using RedditPodcastPoster.Models.Services;
using Xunit;

namespace RedditPodcastPoster.ContentPublisher.Tests.BusinessRules;

/// <summary>#1500: the homepage publishes a curator-removed image as no image plus <c>imageRemoved</c>.</summary>
public class HomepageImageRemovedRules
{
    private static PodcastResult Result(bool imageRemoved, Uri? youTubeImage) => new()
    {
        PodcastName = "Synthetic Podcast",
        EpisodeId = Guid.NewGuid(),
        EpisodeTitle = "Synthetic title",
        EpisodeDescription = "Synthetic description",
        Release = new DateTime(2026, 11, 21, 14, 0, 0, DateTimeKind.Utc),
        Services = new Dictionary<string, ServiceLink>(StringComparer.Ordinal)
        {
            ["youtube"] = new()
            {
                Url = new Uri("https://www.youtube.com/watch?v=syntheticvid"),
                Image = youTubeImage
            }
        },
        ImageRemoved = imageRemoved
    };

    [Fact(DisplayName =
        "Homepage: a curator-removed image publishes no image and imageRemoved=true, so the site shows no art " +
        "instead of the YouTube thumbnail (#1500).")]
    public void removed_image_publishes_no_image_and_the_flag()
    {
        // Arrange
        var result = Result(imageRemoved: true, youTubeImage: null);

        // Act
        var recent = HomepagePublisher.ToRecentEpisode(result);
        var json = JsonDocument.Parse(JsonSerializer.Serialize(recent)).RootElement;

        // Assert
        recent.Image.Should().BeNull();
        recent.ImageRemoved.Should().BeTrue();
        json.GetProperty("imageRemoved").GetBoolean().Should().BeTrue();
    }

    [Fact(DisplayName =
        "Homepage: an episode whose image was not removed omits imageRemoved and keeps its image, so other rows are unchanged (#1500).")]
    public void kept_image_omits_the_flag()
    {
        // Arrange
        var image = new Uri("https://i.ytimg.com/vi/syntheticvid/hqdefault.jpg");
        var result = Result(imageRemoved: false, youTubeImage: image);

        // Act
        var recent = HomepagePublisher.ToRecentEpisode(result);
        var json = JsonDocument.Parse(JsonSerializer.Serialize(recent)).RootElement;

        // Assert
        recent.Image.Should().Be(image);
        recent.ImageRemoved.Should().BeNull();
        json.TryGetProperty("imageRemoved", out _).Should().BeFalse();
    }
}
