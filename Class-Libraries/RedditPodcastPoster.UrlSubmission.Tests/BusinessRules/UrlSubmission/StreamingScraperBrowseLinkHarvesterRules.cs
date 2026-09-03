using FluentAssertions;
using RedditPodcastPoster.UrlSubmission.Tests.Support;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class StreamingScraperBrowseLinkHarvesterRules
{
    [Fact(DisplayName =
        "Browse-page submit URL classification accepts Sounds play and iPlayer episode paths " +
        "and rejects BBC homepage/section paths that are not submit-eligible.")]
    public void bbc_submit_classification_matches_play_and_episode_only()
    {
        // Arrange
        var play = new Uri("https://www.bbc.co.uk/sounds/play/m00289vf");
        var episode = new Uri("https://www.bbc.co.uk/iplayer/episode/m001z8bz/doctor-who");
        var home = new Uri("https://www.bbc.co.uk/sounds");
        var section = new Uri("https://www.bbc.co.uk/iplayer/categories/films/a-z");

        // Act / Assert
        StreamingScraperBrowseLinkHarvester.IsSubmitUrl(StreamingScraperProvider.BbcSounds, play).Should().BeTrue();
        StreamingScraperBrowseLinkHarvester.IsSubmitUrl(StreamingScraperProvider.BbcIplayer, episode).Should().BeTrue();
        StreamingScraperBrowseLinkHarvester.IsSubmitUrl(StreamingScraperProvider.BbcSounds, home).Should().BeFalse();
        StreamingScraperBrowseLinkHarvester.IsSubmitUrl(StreamingScraperProvider.BbcIplayer, section).Should().BeFalse();
    }

    [Fact(DisplayName =
        "Browse pages catalogue lists homepage and section seeds for Sounds, iPlayer, Prime, and Vimeo " +
        "so live harvest tests know where to scrape submit-eligible cards.")]
    public void browse_pages_include_home_and_section_seeds()
    {
        // Arrange / Act
        var pages = StreamingScraperBrowsePages.Pages;

        // Assert
        pages.Should().Contain(p => p.CaseId == "sounds-home");
        pages.Should().Contain(p => p.CaseId == "sounds-music");
        pages.Should().Contain(p => p.CaseId == "iplayer-home");
        pages.Should().Contain(p => p.CaseId == "iplayer-films-az");
        pages.Should().Contain(p => p.CaseId == "prime-storefront-home");
        pages.Should().Contain(p => p.CaseId == "vimeo-home");
        pages.Should().OnlyContain(p => p.MinSubmitLinks >= 1);
    }
}
