using FluentAssertions;
using RedditPodcastPoster.Episodes.Applying;
using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Episodes.Tests.Domain;

public class EpisodePlatformApplierTests
{
    [Fact(DisplayName = "Applier fills missing YouTube URL without replacing an existing URL.")]
    public void ApplyFillMissing_fills_YouTube_URL_when_absent()
    {
        // Given a stored episode with YouTube ID but no URL
        const string youTubeId = "applierYouTube01";
        var youTubeUrl = new Uri($"https://www.youtube.com/watch?v={youTubeId}");
        var target = new Episode
        {
            Title = "Episode title",
            YouTubeId = youTubeId
        };

        // When applying a YouTube platform patch
        var applier = new EpisodePlatformApplier();
        var patch = new EpisodePlatformPatch(
            new PlatformLink(Service.YouTube, youTubeId, youTubeUrl, null),
            null,
            null);

        // Then the missing URL is filled
        applier.ApplyFillMissing(target, patch).Should().BeTrue();
        target.Urls.YouTube.Should().Be(youTubeUrl);
    }
}
