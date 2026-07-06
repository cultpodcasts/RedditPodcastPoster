using FluentAssertions;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.Applying;
using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Assertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Applying;

/// <summary>
/// Shared enrichment applicator rules — catalogue candidates apply via applier without overwriting existing links.
/// </summary>
public class PlatformEnrichmentApplicatorRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly IPlatformEnrichmentApplicator _applicator = EpisodeDomainTestServices.CreateEnrichmentApplicator();
    private readonly SpotifyEpisodeAdapter _spotifyAdapter = new();
    private readonly AppleEpisodeAdapter _appleAdapter = new();

    [Fact(DisplayName =
        "When a stored episode is missing a Spotify link, applying a Spotify catalogue candidate " +
        "fills the platform ID, URL, and image via the applier.")]
    public void apply_fills_missing_spotify_link_from_catalogue_candidate()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var spotifyInput = _fixture.CreateSpotifyCatalogueInput();
        var candidate = _spotifyAdapter.Adapt(spotifyInput);
        var target = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .Customize(e =>
            {
                e.SpotifyId = string.Empty;
                e.Urls = new ServiceUrls();
                e.Images = new EpisodeImages();
            })
            .Create();
        var expected = EpisodeExpectation.From(target)
            .WithSpotify(spotifyInput.SpotifyId, spotifyInput.SpotifyUrl, spotifyInput.Image);

        // Act
        var result = _applicator.Apply(podcast, target, candidate);

        // Assert
        result.Updated.Should().BeTrue();
        result.Service.Should().Be(Service.Spotify);
        target.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "When a stored episode already has a Spotify link, applying a Spotify catalogue candidate " +
        "does not replace the existing platform ID or URL.")]
    public void apply_does_not_replace_existing_spotify_link()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var existingSpotifyId = _fixture.CreateSpotifyId();
        var existingUrl = _fixture.DefaultSpotifyUrl(existingSpotifyId);
        var target = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithSpotify(existingSpotifyId, existingUrl)
            .Create();
        var otherInput = _fixture.CreateSpotifyCatalogueInput();
        var candidate = _spotifyAdapter.Adapt(otherInput);
        var expected = EpisodeExpectation.From(target);

        // Act
        var result = _applicator.Apply(podcast, target, candidate);

        // Assert
        result.Updated.Should().BeFalse();
        target.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "When a stored episode has no description, applying a catalogue candidate fills the description " +
        "from the candidate payload.")]
    public void apply_fills_empty_description_from_catalogue_candidate()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var description = _fixture.Create<string>();
        var spotifyInput = _fixture.CreateSpotifyCatalogueInput(b => b.WithDescription(description));
        var candidate = _spotifyAdapter.Adapt(spotifyInput);
        var target = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .Customize(e =>
            {
                e.Description = string.Empty;
                e.SpotifyId = string.Empty;
                e.Urls = new ServiceUrls();
            })
            .Create();

        // Act
        _applicator.Apply(podcast, target, candidate);

        // Assert
        target.Description.Should().Be(description);
    }

    [Fact(DisplayName =
        "When a stored episode with midnight release is enriched from Apple on the same calendar day, " +
        "the applicator backfills publish time-of-day via merge policy.")]
    public void apply_backfills_apple_release_time_when_stored_release_is_midnight()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        podcast.AppleId = _fixture.CreateAppleId();
        var midnightRelease = DomainTestFixture.UtcDateDaysAgo(2);
        var appleRelease = midnightRelease.AddHours(8);
        var storedLength = _fixture.CreateDuration();
        var storedTitle = _fixture.CreateTitle();
        var spotifyId = _fixture.CreateSpotifyId();
        var target = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .Customize(e =>
            {
                e.Title = storedTitle;
                e.Length = storedLength;
                e.Release = midnightRelease;
                e.SpotifyId = spotifyId;
                e.Urls = new ServiceUrls { Spotify = _fixture.DefaultSpotifyUrl(spotifyId) };
            })
            .Create();
        var appleInput = _fixture.CreateAppleCatalogueInput(b => b
            .WithTitle(DomainTestFixture.CreateFuzzyTitleVariant(storedTitle))
            .WithRelease(appleRelease)
            .WithDuration(storedLength + TimeSpan.FromMinutes(3)));
        var candidate = _appleAdapter.Adapt(appleInput);

        // Act
        var result = _applicator.Apply(podcast, target, candidate);

        // Assert
        result.ReleaseUpdated.Should().BeTrue();
        target.Release.Should().Be(appleRelease);
    }

    [Fact(DisplayName =
        "When a YouTube release authority episode with YouTube identity already has midnight release, " +
        "the applicator preserves YouTube publish time and does not backfill from Apple.")]
    public void apply_preserves_youtube_authoritative_release_when_enriching_from_apple()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        podcast.AppleId = _fixture.CreateAppleId();
        var youTubeRelease = DomainTestFixture.UtcAtTime(-10, _fixture.CreateNonMidnightTimeOfDay());
        var storedLength = _fixture.CreateDuration();
        var storedTitle = _fixture.CreateShortTitle();
        var youTubeId = _fixture.CreateYouTubeId();
        var target = _fixture.CreateStoredEpisodeWithYouTubeOnly(
            podcast,
            youTubeRelease,
            storedLength,
            storedTitle);
        target.YouTubeId = youTubeId;
        target.Urls.YouTube = _fixture.DefaultYouTubeUrl(youTubeId);
        var appleRelease = youTubeRelease.AddHours(8);
        var appleInput = _fixture.CreateAppleCatalogueInput(b => b
            .WithTitle(storedTitle)
            .WithRelease(appleRelease)
            .WithDuration(storedLength));
        var candidate = _appleAdapter.Adapt(appleInput);

        // Act
        var result = _applicator.Apply(podcast, target, candidate);

        // Assert
        result.ReleaseUpdated.Should().BeFalse();
        target.Release.Should().Be(youTubeRelease);
    }

    [Fact(DisplayName =
        "When a supplemental description is applied to an episode with no description, " +
        "ApplyDescription fills the text via the applier contract.")]
    public void apply_description_fills_empty_episode_description()
    {
        // Arrange
        var target = _fixture.BuildEpisode()
            .Customize(e => e.Description = string.Empty)
            .Create();
        var description = _fixture.Create<string>();

        // Act
        var updated = _applicator.ApplyDescription(target, description);

        // Assert
        updated.Should().BeTrue();
        target.Description.Should().Be(description);
    }

    [Fact(DisplayName =
        "When a supplemental YouTube image link is applied, ApplySupplementalLink fills missing artwork " +
        "without replacing an existing platform URL.")]
    public void apply_supplemental_link_fills_missing_youtube_image()
    {
        // Arrange
        var youTubeId = _fixture.CreateYouTubeId();
        var url = _fixture.DefaultYouTubeUrl(youTubeId);
        var image = _fixture.Create<Uri>();
        var target = _fixture.BuildEpisode()
            .Customize(e =>
            {
                e.YouTubeId = youTubeId;
                e.Urls = new ServiceUrls { YouTube = url };
                e.Images = new EpisodeImages();
            })
            .Create();

        // Act
        var updated = _applicator.ApplySupplementalLink(
            target,
            new PlatformLink(Service.YouTube, youTubeId, url, image));

        // Assert
        updated.Should().BeTrue();
        target.Images!.YouTube.Should().Be(image);
    }
}
