using FluentAssertions;
using RedditPodcastPoster.Zdf.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.Zdf.Tests.BusinessRules;

public class ZdfUrlMatcherRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A ZDF /{category}/{programme-slug} URL is a programme submit URL, " +
        "including optional legacy .html suffixes.")]
    public void programme_url_is_submit_url()
    {
        // Arrange
        var slug = $"{_fixture.CreateYouTubeId()}-100";
        var url = new Uri($"https://www.zdf.de/serien/{slug}");
        var legacy = new Uri($"https://www.zdf.de/filme/{slug}.html");

        // Act / Assert
        ZdfUrlMatcher.IsSubmitUrl(url).Should().BeTrue();
        ZdfUrlMatcher.IsProgrammeUrl(url).Should().BeTrue();
        ZdfUrlMatcher.IsSubmitUrl(legacy).Should().BeTrue();
        ZdfUrlMatcher.IsProgrammeUrl(legacy).Should().BeTrue();
    }

    [Fact(DisplayName =
        "A ZDF /video|play/{category}/{programme}/{episode} URL is an episode submit URL, " +
        "because those paths identify a playable catalogue item.")]
    public void video_and_play_urls_are_episode_submit_urls()
    {
        // Arrange
        var programme = $"{_fixture.CreateYouTubeId()}-100";
        var episode = $"{_fixture.CreateYouTubeId()}-110";
        var video = new Uri($"https://www.zdf.de/video/serien/{programme}/{episode}");
        var play = new Uri($"https://www.zdf.de/play/filme/{programme}/{episode}");

        // Act / Assert
        ZdfUrlMatcher.IsSubmitUrl(video).Should().BeTrue();
        ZdfUrlMatcher.IsEpisodeUrl(video).Should().BeTrue();
        ZdfUrlMatcher.IsSubmitUrl(play).Should().BeTrue();
        ZdfUrlMatcher.IsEpisodeUrl(play).Should().BeTrue();
    }

    [Fact(DisplayName =
        "A ZDF marketing root such as /serien, /assets/..., or /impressum is not a submit URL, " +
        "because it is not a programme or episode page.")]
    public void marketing_roots_are_not_submit_urls()
    {
        // Arrange
        var category = new Uri("https://www.zdf.de/serien");
        var assets = new Uri($"https://www.zdf.de/assets/{_fixture.CreateYouTubeId()}-100");
        var impressum = new Uri("https://www.zdf.de/impressum");

        // Act / Assert
        ZdfUrlMatcher.IsSubmitUrl(category).Should().BeFalse();
        ZdfUrlMatcher.IsSubmitUrl(assets).Should().BeFalse();
        ZdfUrlMatcher.IsSubmitUrl(impressum).Should().BeFalse();
    }

    [Fact(DisplayName =
        "A lookalike host that merely contains the letters zdf.de is not a submit URL, " +
        "because host matching is suffix-safe.")]
    public void lookalike_host_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.zdf.de.example.test/serien/{_fixture.CreateYouTubeId()}-100");

        // Act
        var matches = ZdfUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }
}
