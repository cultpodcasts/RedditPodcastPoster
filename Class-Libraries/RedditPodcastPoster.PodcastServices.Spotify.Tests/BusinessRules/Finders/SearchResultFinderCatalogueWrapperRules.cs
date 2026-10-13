using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.TitleCasing;
using RedditPodcastPoster.PodcastServices.Spotify.Finders;
using RedditPodcastPoster.PodcastServices.Spotify.Tests.Fakes;
using RedditPodcastPoster.Text.Sanitisers;
using RedditPodcastPoster.Text.TitleCasing;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Finders;

/// <summary>
/// Thin-wrapper rules: Spotify finder delegates catalogue matching to the domain matcher.
/// </summary>
public class SearchResultFinderCatalogueWrapperRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly SpotifySearchResultFinder _sut = new(
        EpisodeDomainTestServices.CreatePlatformMatcher(),
        new HtmlSanitiser(NullLogger<HtmlSanitiser>.Instance));

    [Fact(DisplayName =
        "When the Spotify finder resolves by release date, " +
        "it returns the SimpleEpisode whose title and calendar date match the probe.")]
    public void find_by_date_delegates_to_domain_matcher_and_maps_back()
    {
        // Arrange
        var sharedTitle = _fixture.CreateTitle();
        var release = DomainTestFixture.UtcDateDaysAgo(7);
        var spotifyId = _fixture.CreateSpotifyId();
        var episodes = new List<SimpleEpisode>
        {
            new()
            {
                Id = spotifyId,
                Name = sharedTitle,
                DurationMs = (int)_fixture.CreateDuration().TotalMilliseconds,
                ReleaseDate = release.ToString("yyyy-MM-dd")
            }
        };

        // Act
        var result = _sut.FindMatchingEpisodeByDate(sharedTitle, release, episodes);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(spotifyId);
    }

}
