using FluentAssertions;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Matching;

/// <summary>
/// Catalogue lookup matching rules consolidated from platform finders (Phase D).
/// </summary>
public class CatalogueMatchingRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly IEpisodePlatformMatcher _matcher = EpisodeDomainTestServices.CreatePlatformMatcher();

    [Fact(DisplayName =
        "When catalogue titles differ but only one candidate shares the probe duration, " +
        "the unique-duration path accepts the match without a title match.")]
    public void unique_duration_without_title_match_selects_sole_duration_candidate()
    {
        // Arrange
        var probeLength = _fixture.CreateDuration();
        var probeTitle = _fixture.CreateTitle();
        var otherLength = probeLength + TimeSpan.FromMinutes(30);
        var sharedRelease = DomainTestFixture.UtcDateDaysAgo(3);
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = probeTitle;
            e.Length = probeLength;
            e.Release = sharedRelease;
        });
        var matchingCandidate = _fixture.CreateEpisode(e =>
        {
            e.Title = _fixture.CreateTitle();
            e.Length = probeLength;
            e.Release = sharedRelease;
            e.SpotifyId = _fixture.CreateSpotifyId();
        });
        var otherCandidate = _fixture.CreateEpisode(e =>
        {
            e.Title = _fixture.CreateTitle();
            e.Length = otherLength;
            e.Release = sharedRelease;
            e.SpotifyId = _fixture.CreateSpotifyId();
        });
        var podcast = _fixture.CreatePodcast();

        // Act
        var result = _matcher.FindCatalogueMatchByLength(
            probe,
            [matchingCandidate, otherCandidate],
            podcast,
            episodeMatchRegex: null,
            new CatalogueMatchByLengthOptions(AcceptUniqueDurationWithoutTitleMatch: true));

        // Assert
        result.Should().BeSameAs(matchingCandidate);
    }

    [Fact(DisplayName =
        "When probe and catalogue item titles overlap by substring, " +
        "the longest overlapping title wins among multiple substring matches.")]
    public void substring_title_match_prefers_longest_title()
    {
        // Arrange
        var sharedCore = _fixture.CreateShortTitle();
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = sharedCore;
            e.Length = _fixture.CreateDuration();
            e.Release = DomainTestFixture.UtcDateDaysAgo(2);
        });
        var shorter = _fixture.CreateEpisode(e =>
        {
            e.Title = sharedCore.Split(' ')[0];
            e.Length = probe.Length;
            e.Release = probe.Release;
            e.SpotifyId = _fixture.CreateSpotifyId();
        });
        var longer = _fixture.CreateEpisode(e =>
        {
            e.Title = $"{sharedCore} extended suffix words";
            e.Length = probe.Length;
            e.Release = probe.Release;
            e.SpotifyId = _fixture.CreateSpotifyId();
        });
        var podcast = _fixture.CreatePodcast();

        // Act
        var result = _matcher.FindCatalogueMatchByLength(
            probe,
            [shorter, longer],
            podcast,
            episodeMatchRegex: null,
            new CatalogueMatchByLengthOptions());

        // Assert
        result.Should().BeSameAs(longer);
    }

    [Fact(DisplayName =
        "When probe release aligns with catalogue release within the date-only window, " +
        "FindCatalogueMatchByDate selects the matching catalogue item.")]
    public void date_match_accepts_same_calendar_date()
    {
        // Arrange
        var sharedTitle = _fixture.CreateTitle();
        var release = DomainTestFixture.UtcDateDaysAgo(4);
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = sharedTitle;
            e.Release = release;
        });
        var matching = _fixture.CreateEpisode(e =>
        {
            e.Title = sharedTitle;
            e.Release = release;
            e.SpotifyId = _fixture.CreateSpotifyId();
        });
        var podcast = _fixture.CreatePodcast();

        // Act
        var result = _matcher.FindCatalogueMatchByDate(
            probe,
            [matching],
            podcast,
            episodeMatchRegex: null);

        // Assert
        result.Should().BeSameAs(matching);
    }

    [Fact(DisplayName =
        "When filtering catalogue candidates for platform lookup, " +
        "CatalogueReleaseMatches delegates to Spotify catalogue release tolerance.")]
    public void catalogue_release_filter_uses_spotify_catalogue_tolerance()
    {
        // Arrange
        const int youTubeReleaseDaysAgo = 30;
        const int spotifyDaysAfterYouTube = 28;
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var youTubeRelease = DomainTestFixture.UtcAtTime(
            -youTubeReleaseDaysAgo,
            _fixture.CreateNonMidnightTimeOfDay());
        var storedLength = _fixture.CreateDuration();
        var stored = _fixture.CreateStoredEpisodeWithYouTubeOnly(
            podcast,
            youTubeRelease,
            storedLength);
        var lookupRelease = youTubeRelease.Subtract(podcast.YouTubePublishingDelay());
        var spotifyRelease = DomainTestFixture.SpotifyCatalogueReleaseDaysAfterYouTube(
            youTubeRelease,
            spotifyDaysAfterYouTube);
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = stored.Title;
            e.Length = storedLength;
            e.Release = lookupRelease;
        });
        var catalogueItem = _fixture.CreateEpisode(e =>
        {
            e.Title = _fixture.CreateTitle();
            e.Length = storedLength;
            e.Release = spotifyRelease;
            e.SpotifyId = _fixture.CreateSpotifyId();
        });

        // Act
        var matches = _matcher.CatalogueReleaseMatches(probe, catalogueItem, podcast);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When the probe episode has no release date, " +
        "CatalogueReleaseMatches rejects the catalogue item for platform lookup filtering.")]
    public void catalogue_release_filter_rejects_probe_without_release()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = _fixture.CreateTitle();
            e.Length = _fixture.CreateDuration();
            e.Release = DateTime.MinValue;
        });
        var catalogueItem = _fixture.CreateEpisode(e =>
        {
            e.Title = _fixture.CreateTitle();
            e.Length = probe.Length;
            e.Release = DomainTestFixture.UtcDateDaysAgo(5);
            e.SpotifyId = _fixture.CreateSpotifyId();
        });

        // Act
        var matches = _matcher.CatalogueReleaseMatches(probe, catalogueItem, podcast);

        // Assert
        matches.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When a catalogue item's release falls outside the Spotify catalogue tolerance window, " +
        "CatalogueReleaseMatches rejects it for platform lookup filtering.")]
    public void catalogue_release_filter_rejects_release_outside_tolerance()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var youTubeRelease = DomainTestFixture.UtcAtTime(-10, _fixture.CreateNonMidnightTimeOfDay());
        var lookupRelease = youTubeRelease.Subtract(podcast.YouTubePublishingDelay());
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = _fixture.CreateTitle();
            e.Length = _fixture.CreateDuration();
            e.Release = lookupRelease;
        });
        var farOffRelease = lookupRelease.AddDays(-30);
        var catalogueItem = _fixture.CreateEpisode(e =>
        {
            e.Title = _fixture.CreateTitle();
            e.Length = probe.Length;
            e.Release = farOffRelease;
            e.SpotifyId = _fixture.CreateSpotifyId();
        });

        // Act
        var matches = _matcher.CatalogueReleaseMatches(probe, catalogueItem, podcast);

        // Assert
        matches.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When enriching a YouTube-discovered episode and multiple catalogue rows share its duration, " +
        "FindCatalogueMatchByLength selects the row whose release is closest to the probe.")]
    public void youtube_discovered_multiple_same_length_picks_closest_release()
    {
        // Arrange
        var probeLength = TimeSpan.FromMinutes(54) + TimeSpan.FromSeconds(30);
        var probeRelease = DomainTestFixture.UtcAtTime(-5, _fixture.CreateNonMidnightTimeOfDay());
        var closerRelease = probeRelease.AddHours(-2);
        var fartherRelease = probeRelease.AddDays(-3);
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = _fixture.CreateTitle();
            e.Length = probeLength;
            e.Release = probeRelease;
        });
        var closerCandidate = _fixture.CreateEpisode(e =>
        {
            e.Title = _fixture.CreateTitle();
            e.Length = probeLength + TimeSpan.FromSeconds(10);
            e.Release = closerRelease;
            e.AppleId = _fixture.CreateAppleId();
        });
        var fartherCandidate = _fixture.CreateEpisode(e =>
        {
            e.Title = _fixture.CreateTitle();
            e.Length = probeLength - TimeSpan.FromSeconds(15);
            e.Release = fartherRelease;
            e.AppleId = _fixture.CreateAppleId();
        });
        var podcast = _fixture.CreatePodcast();

        // Act
        var result = _matcher.FindCatalogueMatchByLength(
            probe,
            [fartherCandidate, closerCandidate],
            podcast,
            episodeMatchRegex: null,
            new CatalogueMatchByLengthOptions(EnrichingYouTubeDiscoveredEpisode: true));

        // Assert
        result.Should().BeSameAs(closerCandidate);
    }

    [Fact(DisplayName =
        "For YouTube release authority catalogue lookup, " +
        "FindCatalogueMatchByLength may match on broader duration tolerance when titles differ by typo.")]
    public void release_authority_youtube_uses_broader_duration_tolerance_for_typo_titles()
    {
        // Arrange
        var storedTitle = _fixture.CreateShortTitle();
        var incomingTitle = DomainTestFixture.CreateTypoTitleVariant(storedTitle);
        var probeLength = _fixture.CreateDuration();
        var catalogueLength = probeLength + TimeSpan.FromSeconds(45);
        var sharedRelease = DomainTestFixture.UtcAtTime(-4, _fixture.CreateNonMidnightTimeOfDay());
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = storedTitle;
            e.Length = probeLength;
            e.Release = sharedRelease;
        });
        var matchingCandidate = _fixture.CreateEpisode(e =>
        {
            e.Title = incomingTitle;
            e.Length = catalogueLength;
            e.Release = sharedRelease;
            e.SpotifyId = _fixture.CreateSpotifyId();
        });
        var podcast = _fixture.CreatePodcast();

        // Act
        var result = _matcher.FindCatalogueMatchByLength(
            probe,
            [matchingCandidate],
            podcast,
            episodeMatchRegex: null,
            new CatalogueMatchByLengthOptions(ReleaseAuthority: Service.YouTube));

        // Assert
        result.Should().BeSameAs(matchingCandidate);
    }

    [Fact(DisplayName =
        "When probe and catalogue releases differ by one calendar day and titles differ by typo, " +
        "FindCatalogueMatchByDate may still select the catalogue item.")]
    public void date_match_accepts_adjacent_day_with_fuzzy_title()
    {
        // Arrange
        var storedTitle = _fixture.CreateShortTitle();
        var incomingTitle = DomainTestFixture.CreateTypoTitleVariant(storedTitle);
        var probeRelease = DomainTestFixture.UtcDateDaysAgo(6);
        var catalogueRelease = probeRelease.AddDays(1);
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = storedTitle;
            e.Release = probeRelease;
        });
        var matching = _fixture.CreateEpisode(e =>
        {
            e.Title = incomingTitle;
            e.Release = catalogueRelease;
            e.SpotifyId = _fixture.CreateSpotifyId();
        });
        var podcast = _fixture.CreatePodcast();

        // Act
        var result = _matcher.FindCatalogueMatchByDate(
            probe,
            [matching],
            podcast,
            episodeMatchRegex: null);

        // Assert
        result.Should().BeSameAs(matching);
    }

    [Fact(DisplayName =
        "When a catalogue lookup reducer excludes assigned platform IDs, " +
        "FindCatalogueMatchByLength does not return excluded candidates.")]
    public void catalogue_reducer_excludes_assigned_platform_ids()
    {
        // Arrange
        var sharedTitle = _fixture.CreateTitle();
        var sharedLength = _fixture.CreateDuration();
        var sharedRelease = DomainTestFixture.UtcDateDaysAgo(3);
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = sharedTitle;
            e.Length = sharedLength;
            e.Release = sharedRelease;
        });
        var assignedId = _fixture.CreateSpotifyId();
        var availableId = _fixture.CreateSpotifyId();
        var excluded = _fixture.CreateEpisode(e =>
        {
            e.Title = sharedTitle;
            e.Length = sharedLength;
            e.Release = sharedRelease;
            e.SpotifyId = assignedId;
        });
        var available = _fixture.CreateEpisode(e =>
        {
            e.Title = sharedTitle;
            e.Length = sharedLength;
            e.Release = sharedRelease;
            e.SpotifyId = availableId;
        });
        var assignedIds = new HashSet<string> { assignedId };
        var podcast = _fixture.CreatePodcast();

        // Act
        var result = _matcher.FindCatalogueMatchByLength(
            probe,
            [excluded, available],
            podcast,
            episodeMatchRegex: null,
            new CatalogueMatchByLengthOptions(),
            e => string.IsNullOrWhiteSpace(e.SpotifyId) || !assignedIds.Contains(e.SpotifyId));

        // Assert
        result.Should().BeSameAs(available);
    }

    [Fact(DisplayName =
        "For YouTube release authority podcasts with positive publishing delay, " +
        "IsCatalogueMatch accepts a YouTube catalogue item whose publish aligns after delay adjustment.")]
    public void is_catalogue_match_accepts_positive_delay_aligned_youtube_item()
    {
        // Arrange
        var publishingDelay = TimeSpan.FromDays(1);
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcast(
            _fixture.CreateYouTubeChannelId(),
            publishingDelay.Ticks);
        var audioRelease = DomainTestFixture.UtcAtTime(-2, _fixture.CreateNonMidnightTimeOfDay());
        var sharedLength = _fixture.CreateDuration();
        var sharedTitle = _fixture.CreateShortTitle();
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = sharedTitle;
            e.Length = sharedLength;
            e.Release = audioRelease;
        });
        var youTubeInput = _fixture.CreateYouTubeCatalogueInput(b => b
            .WithTitle(sharedTitle)
            .WithRelease(audioRelease.Add(publishingDelay))
            .WithDuration(sharedLength));
        var catalogueItem = _fixture.CreateEpisode(e =>
        {
            e.Title = youTubeInput.Title;
            e.Length = youTubeInput.Duration;
            e.Release = youTubeInput.Release;
            e.YouTubeId = youTubeInput.YouTubeId;
        });

        // Act
        var matches = _matcher.IsCatalogueMatch(probe, catalogueItem, podcast, episodeMatchRegex: null);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "For YouTube release authority podcasts with negative publishing delay, " +
        "IsCatalogueMatch does not treat clearly different titles as the same catalogue item.")]
    public void is_catalogue_match_rejects_negative_delay_when_titles_clearly_differ()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var (stored, discovered) = _fixture.CreateNegativeDelayNonMatchingPair(podcast);

        // Act
        var matches = _matcher.IsCatalogueMatch(stored, discovered, podcast, episodeMatchRegex: null);

        // Assert
        matches.Should().BeFalse();
    }
}
