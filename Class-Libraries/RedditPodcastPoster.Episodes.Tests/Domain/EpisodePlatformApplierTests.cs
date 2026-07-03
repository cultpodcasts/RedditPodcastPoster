using FluentAssertions;
using RedditPodcastPoster.Episodes.Applying;
using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Episodes.Tests.Domain;

public class EpisodePlatformApplierTests
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName = "Applier fills missing YouTube URL without replacing an existing URL.")]
    public void ApplyFillMissing_fills_YouTube_URL_when_absent()
    {
        // Given a stored episode with YouTube ID but no URL
        var youTubeInput = _fixture.CreateYouTubeCatalogueInput();
        var target = new Episode
        {
            Title = "Episode title",
            YouTubeId = youTubeInput.YouTubeId
        };

        // When applying a YouTube platform patch
        var applier = new EpisodePlatformApplier();
        var patch = new EpisodePlatformPatch(
            new PlatformLink(Service.YouTube, youTubeInput.YouTubeId, youTubeInput.YouTubeUrl, null),
            null,
            null);

        // Then the missing URL is filled
        applier.ApplyFillMissing(target, patch).Should().BeTrue();
        target.Urls.YouTube.Should().Be(youTubeInput.YouTubeUrl);
    }
}
