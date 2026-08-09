using FluentAssertions;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Assertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Matching;

/// <summary>
/// Catalogue lookup matching rules consolidated from platform finders (Phase D).
/// </summary>
public class CatalogueMatchingRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly IEpisodePlatformMatcher _matcher = EpisodeDomainTestServices.CreatePlatformMatcher();
    private readonly EpisodeMerger _merger = EpisodeDomainTestServices.CreateMerger();

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
        "When a catalogue item's release falls before the inferred YouTube day for a negative-delay " +
        "podcast, CatalogueReleaseMatches rejects it for platform lookup filtering.")]
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
        var farOffRelease = youTubeRelease.AddDays(-5);
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
        var probeTitle = _fixture.CreateShortTitle();
        var probeLength = TimeSpan.FromMinutes(54) + TimeSpan.FromSeconds(30);
        var probeRelease = DomainTestFixture.UtcAtTime(-5, _fixture.CreateNonMidnightTimeOfDay());
        var closerRelease = probeRelease.AddHours(-2);
        var fartherRelease = probeRelease.AddDays(-3);
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = probeTitle;
            e.Length = probeLength;
            e.Release = probeRelease;
        });
        var closerCandidate = _fixture.CreateEpisode(e =>
        {
            e.Title = DomainTestFixture.CreateTypoTitleVariant(probeTitle);
            e.Length = probeLength + TimeSpan.FromSeconds(10);
            e.Release = closerRelease;
            e.AppleId = _fixture.CreateAppleId();
        });
        var fartherCandidate = _fixture.CreateEpisode(e =>
        {
            e.Title = DomainTestFixture.CreateTypoTitleVariant(probeTitle);
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
        "IsCatalogueMatch does not treat episodes as the same when weak catalogue-day release alignment " +
        "plus similar duration lack fuzzy or substring title confidence.")]
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

    
    [Fact(DisplayName =
        "For YouTube release authority podcasts with negative publishing delay, IsCatalogueMatch accepts " +
        "delay-aligned Apple/Spotify catalogue rows for a YouTube-only stored episode even when marketing " +
        "titles are wholly divergent — composite score meets the cross-platform threshold.")]
    public void is_catalogue_match_accepts_negative_delay_aligned_divergent_titles()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var (stored, discovered, _) = _fixture.CreateNegativeDelayAlignedDivergentTitlePair(podcast);

        // Act
        var matches = _matcher.IsCatalogueMatch(stored, discovered, podcast, episodeMatchRegex: null);

        // Assert
        matches.Should().BeTrue();
    }

[Fact(DisplayName =
        "For YouTube release authority podcasts with negative publishing delay, " +
        "IsCatalogueMatch accepts an aligned Spotify catalogue item for a YouTube-only stored episode.")]
    public void is_catalogue_match_accepts_negative_delay_aligned_spotify_catalogue()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var (stored, discovered, _) = _fixture.CreateCrossPlatformYouTubeReleaseAuthorityPair(podcast);

        // Act
        var matches = _matcher.IsCatalogueMatch(stored, discovered, podcast, episodeMatchRegex: null);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "For YouTube release authority podcasts with positive publishing delay, " +
        "IsCatalogueMatch rejects a YouTube catalogue item whose publish exceeds the delay-alignment threshold " +
        "when titles do not exactly match.")]
    public void is_catalogue_match_rejects_positive_delay_misaligned_youtube_catalogue()
    {
        // Arrange
        var publishingDelay = TimeSpan.FromDays(1);
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcast(
            _fixture.CreateYouTubeChannelId(),
            publishingDelay.Ticks);
        var audioRelease = DomainTestFixture.UtcAtTime(-2, _fixture.CreateNonMidnightTimeOfDay());
        var sharedLength = _fixture.CreateDuration();
        var probeTitle = "The Economics of Cheese";
        var catalogueTitle = "Quantum Gardening Weekly";
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = probeTitle;
            e.Length = sharedLength;
            e.Release = audioRelease;
        });
        var youTubeInput = _fixture.CreateYouTubeCatalogueInput(b => b
            .WithTitle(catalogueTitle)
            .WithRelease(audioRelease.Add(publishingDelay).AddDays(2))
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
        matches.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When probe and catalogue item share an exact title, IsCatalogueMatch accepts the match " +
        "even when release and duration clearly differ.")]
    public void exact_title_match_accepts_despite_mismatched_release_and_duration()
    {
        // KNOWN: likely wrong-merge risk — exact title short-circuits before release tolerance
        // (EpisodePlatformMatcher.MatchesByTitleHeuristics lines 67–69; pre-soak characterization)
        // Arrange
        var sharedTitle = _fixture.CreateTitle();
        var probeLength = _fixture.CreateDuration();
        var catalogueLength = probeLength + TimeSpan.FromMinutes(30);
        var probeRelease = DomainTestFixture.UtcDateDaysAgo(30);
        var catalogueRelease = DomainTestFixture.UtcDateDaysAgo(2);
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = sharedTitle;
            e.Length = probeLength;
            e.Release = probeRelease;
        });
        var catalogueItem = _fixture.CreateEpisode(e =>
        {
            e.Title = sharedTitle;
            e.Length = catalogueLength;
            e.Release = catalogueRelease;
            e.SpotifyId = _fixture.CreateSpotifyId();
        });
        var podcast = _fixture.CreatePodcast();

        // Act
        var matches = _matcher.IsCatalogueMatch(probe, catalogueItem, podcast, episodeMatchRegex: null);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When enriching a YouTube-discovered episode and both sides have duration outside the proportional band, " +
        "FindCatalogueMatchByLength returns null even when a typo-aligned title sits within twelve hours of release, " +
        "because multi-criteria scoring requires the duration band when both lengths are present.")]
    public void youtube_discovered_release_only_outside_duration_band_returns_null()
    {
        // Arrange — 20m gap >> max(5m, 10% of 40m)
        var sharedTitle = _fixture.CreateShortTitle();
        var probeLength = TimeSpan.FromMinutes(60);
        var mismatchedLength = TimeSpan.FromMinutes(40);
        var probeRelease = DomainTestFixture.UtcAtTime(-4, _fixture.CreateNonMidnightTimeOfDay());
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = sharedTitle;
            e.Length = probeLength;
            e.Release = probeRelease;
        });
        var releaseAlignedCandidate = _fixture.CreateEpisode(e =>
        {
            e.Title = DomainTestFixture.CreateTypoTitleVariant(sharedTitle);
            e.Length = mismatchedLength;
            e.Release = probeRelease.AddHours(6);
            e.AppleId = _fixture.CreateAppleId();
        });
        var podcast = _fixture.CreatePodcast();

        // Act
        var result = _matcher.FindCatalogueMatchByLength(
            probe,
            [releaseAlignedCandidate],
            podcast,
            episodeMatchRegex: null,
            new CatalogueMatchByLengthOptions(EnrichingYouTubeDiscoveredEpisode: true));

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When enriching a YouTube-discovered episode and Apple omits catalogue duration, " +
        "FindCatalogueMatchByLength still returns the title-containment candidate, " +
        "because missing duration must not empty the match when titles align.")]
    public void youtube_discovered_missing_catalogue_duration_title_containment_matches()
    {
        // Arrange
        var youTubeTitle = _fixture.CreateTitle();
        var probeLength = _fixture.CreateDuration();
        var probeRelease = DomainTestFixture.UtcAtTime(-2, _fixture.CreateNonMidnightTimeOfDay());
        var appleId = _fixture.CreateAppleId();
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = youTubeTitle;
            e.Length = probeLength;
            e.Release = probeRelease;
            e.YouTubeId = _fixture.CreateYouTubeId();
        });
        var appleCandidate = _fixture.CreateEpisode(e =>
        {
            e.Title = $"{youTubeTitle}: editorial Apple rename";
            e.Length = TimeSpan.Zero;
            e.Release = probeRelease.AddHours(-1);
            e.AppleId = appleId;
        });
        var podcast = _fixture.CreatePodcast();

        // Act
        var result = _matcher.FindCatalogueMatchByLength(
            probe,
            [appleCandidate],
            podcast,
            episodeMatchRegex: null,
            new CatalogueMatchByLengthOptions(EnrichingYouTubeDiscoveredEpisode: true));

        // Assert
        result.Should().NotBeNull();
        result!.AppleId.Should().Be(appleId);
    }

    [Fact(DisplayName =
        "When enriching a YouTube-discovered episode, FindCatalogueMatchByLength must not duration-snipe " +
        "a catalogue row outside the expanded release window whose title shares no relationship.")]
    public void youtube_discovered_enrichment_does_not_duration_snipe_disjoint_titles()
    {
        // Arrange — YouTube-authority enrichment: titles refer to different interviews; release is days apart
        const string storedTitle =
            "Guest Answers Live Questions About A Political Figure And An Identity Foundation";
        const string catalogueTitle =
            "A Decade Inside An Arranged Marriage And The Exit That Followed";
        var sharedLength = TimeSpan.FromMinutes(80);
        var probeRelease = DomainTestFixture.UtcAtTime(-30, TimeSpan.FromHours(7));
        var catalogueRelease = probeRelease.AddDays(-5);
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = storedTitle;
            e.Length = sharedLength;
            e.Release = probeRelease;
            e.YouTubeId = _fixture.CreateYouTubeId();
        });
        var catalogueItem = _fixture.CreateEpisode(e =>
        {
            e.Title = catalogueTitle;
            e.Length = sharedLength + TimeSpan.FromMinutes(3);
            e.Release = catalogueRelease;
            e.AppleId = _fixture.CreateAppleId();
        });
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();

        // Act
        var result = _matcher.FindCatalogueMatchByLength(
            probe,
            [catalogueItem],
            podcast,
            episodeMatchRegex: null,
            new CatalogueMatchByLengthOptions(EnrichingYouTubeDiscoveredEpisode: true));

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "Incident (Aug 2026): when enriching a YouTube-discovered episode, duration plus same-calendar-day release " +
        "with disjoint titles and empty subjects scores below the multi-criteria threshold and must not match.")]
    public void youtube_discovered_duration_and_day_with_disjoint_titles_and_empty_subjects_returns_null()
    {
        // Arrange - duration+day alone is below MatchThreshold; explicit disjoint titles avoid fuzzy flukes
        const string storedTitle =
            "Guest Answers Live Questions About A Political Figure And An Identity Foundation";
        const string spotifyTitle =
            "A Decade Inside An Arranged Marriage And The Exit That Followed";
        var sharedLength = TimeSpan.FromMinutes(58) + TimeSpan.FromSeconds(34);
        var probeRelease = DomainTestFixture.UtcAtTime(-2, new TimeSpan(19, 0, 0));
        var spotifyRelease = DomainTestFixture.SameCalendarDateWithTime(probeRelease, new TimeSpan(12, 0, 0));
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = storedTitle;
            e.Length = sharedLength;
            e.Release = probeRelease;
            e.YouTubeId = _fixture.CreateYouTubeId();
            e.Subjects = [];
        });
        var catalogueItem = _fixture.CreateEpisode(e =>
        {
            e.Title = spotifyTitle;
            e.Length = sharedLength;
            e.Release = spotifyRelease;
            e.SpotifyId = _fixture.CreateSpotifyId();
            e.Subjects = [];
        });
        var podcast = _fixture.CreatePodcast();

        // Act
        var result = _matcher.FindCatalogueMatchByLength(
            probe,
            [catalogueItem],
            podcast,
            episodeMatchRegex: null,
            new CatalogueMatchByLengthOptions(EnrichingYouTubeDiscoveredEpisode: true));

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "Incident (Aug 2026): a date-only Spotify catalogue row from the same calendar day with unique " +
        "duration but disjoint titles and empty subjects must not match a YouTube-discovered probe - " +
        "duration+day alone stays below the multi-criteria threshold even when midnight Spotify sits " +
        "more than twelve hours from publish.")]
    public void youtube_discovered_spotify_date_only_same_day_duration_without_extra_signals_returns_null()
    {
        // Arrange - Spotify carries no time-of-day, so its midnight release is 21 hours from the video
        const string youTubeTitle =
            "Guest Answers Live Questions About A Political Figure And An Identity Foundation";
        const string spotifyTitle =
            "A Decade Inside An Arranged Marriage And The Exit That Followed";
        var youTubePublish = DomainTestFixture.UtcAtTime(-3, new TimeSpan(21, 0, 0));
        var spotifyRelease = DomainTestFixture.SpotifyCatalogueReleaseDaysAfterYouTube(youTubePublish, 0);
        var sharedLength = _fixture.CreateDuration();
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = youTubeTitle;
            e.Length = sharedLength;
            e.Release = youTubePublish;
            e.YouTubeId = _fixture.CreateYouTubeId();
            e.Subjects = [];
        });
        var spotifyCatalogueItem = _fixture.CreateEpisode(e =>
        {
            e.Title = spotifyTitle;
            e.Length = sharedLength;
            e.Release = spotifyRelease;
            e.SpotifyId = _fixture.CreateSpotifyId();
            e.Subjects = [];
        });
        var podcast = _fixture.CreatePodcast();

        // Act
        var result = _matcher.FindCatalogueMatchByLength(
            probe,
            [spotifyCatalogueItem],
            podcast,
            episodeMatchRegex: null,
            new CatalogueMatchByLengthOptions(EnrichingYouTubeDiscoveredEpisode: true));

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "Incident (Aug 2026): when a negative YouTube publishing delay shifts an older YouTube probe " +
        "onto a newer Spotify catalogue day, a sole similar-duration Spotify row with a disjoint title " +
        "and empty subjects must not attach - that is how the wrong Spotify URL landed on a prior episode.")]
    public void youtube_discovered_delay_aligned_wrong_episode_rejects_without_multi_criteria_confidence()
    {
        // Arrange - older YouTube episode vs later Spotify with similar length and unrelated title;
        // probe release is already delay-adjusted (as FindSpotifyEpisodeRequestFactory would set)
        const string olderYouTubeTitle =
            "Guest Answers Live Questions About A Political Figure And An Identity Foundation";
        const string newerSpotifyTitle =
            "The World Mission Society Church Of God With A Guest A Decade Inside An Arranged Marriage";
        var olderYouTubeLength = TimeSpan.FromMinutes(64) + TimeSpan.FromSeconds(8);
        var newerSpotifyLength = TimeSpan.FromMinutes(62) + TimeSpan.FromSeconds(48);
        var delayAdjustedProbeRelease = DomainTestFixture.UtcDateDaysAgo(0);
        var newerSpotifyRelease = DomainTestFixture.UtcDateDaysAgo(0);
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = olderYouTubeTitle;
            e.Length = olderYouTubeLength;
            e.Release = delayAdjustedProbeRelease;
            e.YouTubeId = _fixture.CreateYouTubeId();
            e.Subjects = [];
        });
        var wrongCatalogueItem = _fixture.CreateEpisode(e =>
        {
            e.Title = newerSpotifyTitle;
            e.Length = newerSpotifyLength;
            e.Release = newerSpotifyRelease;
            e.SpotifyId = _fixture.CreateSpotifyId();
            e.Subjects = [];
        });
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();

        // Act
        var result = _matcher.FindCatalogueMatchByLength(
            probe,
            [wrongCatalogueItem],
            podcast,
            episodeMatchRegex: null,
            new CatalogueMatchByLengthOptions(EnrichingYouTubeDiscoveredEpisode: true));

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When enriching a YouTube-discovered episode with duration plus same-calendar-day release and " +
        "disjoint titles, a single shared classified subject on both Episode.Subjects lifts the score " +
        "to the multi-criteria threshold and selects the catalogue row.")]
    public void youtube_discovered_duration_day_and_shared_subject_matches()
    {
        // Arrange - duration+day alone is below threshold; one shared subject supplies the rest
        const string youTubeTitle =
            "Guest Answers Live Questions About A Political Figure And An Identity Foundation";
        const string spotifyTitle =
            "A Decade Inside An Arranged Marriage And The Exit That Followed";
        var sharedSubject = _fixture.CreateTitle(3);
        var sharedLength = TimeSpan.FromMinutes(58) + TimeSpan.FromSeconds(34);
        var probeRelease = DomainTestFixture.UtcAtTime(-2, new TimeSpan(19, 0, 0));
        var spotifyRelease = DomainTestFixture.SameCalendarDateWithTime(probeRelease, new TimeSpan(12, 0, 0));
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = youTubeTitle;
            e.Length = sharedLength;
            e.Release = probeRelease;
            e.YouTubeId = _fixture.CreateYouTubeId();
            e.Subjects = [sharedSubject];
        });
        var catalogueItem = _fixture.CreateEpisode(e =>
        {
            e.Title = spotifyTitle;
            e.Length = sharedLength;
            e.Release = spotifyRelease;
            e.SpotifyId = _fixture.CreateSpotifyId();
            e.Subjects = [sharedSubject];
        });
        var podcast = _fixture.CreatePodcast();

        // Act
        var result = _matcher.FindCatalogueMatchByLength(
            probe,
            [catalogueItem],
            podcast,
            episodeMatchRegex: null,
            new CatalogueMatchByLengthOptions(EnrichingYouTubeDiscoveredEpisode: true));

        // Assert
        result.Should().BeSameAs(catalogueItem);
    }

    [Fact(DisplayName =
        "When enriching a YouTube-discovered episode, a sole same-day Spotify catalogue row whose title " +
        "shares containment confidence still matches on unique duration despite midnight date-only release.")]
    public void youtube_discovered_spotify_date_only_same_day_matches_with_title_confidence()
    {
        // Arrange — Spotify title contains the YouTube title (containment confidence);
        // Spotify midnight vs late YouTube publish
        var youTubeTitle = _fixture.CreateTitle(4);
        var youTubePublish = DomainTestFixture.UtcAtTime(-3, new TimeSpan(21, 0, 0));
        var spotifyRelease = DomainTestFixture.SpotifyCatalogueReleaseDaysAfterYouTube(youTubePublish, 0);
        var sharedLength = _fixture.CreateDuration();
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = youTubeTitle;
            e.Length = sharedLength;
            e.Release = youTubePublish;
            e.YouTubeId = _fixture.CreateYouTubeId();
        });
        var spotifyCatalogueItem = _fixture.CreateEpisode(e =>
        {
            e.Title = $"{youTubeTitle}: a decade inside and the exit that followed";
            e.Length = sharedLength;
            e.Release = spotifyRelease;
            e.SpotifyId = _fixture.CreateSpotifyId();
        });
        var podcast = _fixture.CreatePodcast();

        // Act
        var result = _matcher.FindCatalogueMatchByLength(
            probe,
            [spotifyCatalogueItem],
            podcast,
            episodeMatchRegex: null,
            new CatalogueMatchByLengthOptions(EnrichingYouTubeDiscoveredEpisode: true));

        // Assert
        result.Should().BeSameAs(spotifyCatalogueItem);
    }

    [Fact(DisplayName =
        "When enriching a YouTube-discovered episode with both EnrichingYouTubeDiscoveredEpisode and " +
        "AcceptUniqueDurationWithoutTitleMatch set (legacy Spotify finder combo), indexing must still " +
        "refuse a wrong-week Spotify catalogue row within five minutes whose title is wholly disjoint.")]
    public void youtube_enrichment_with_unique_duration_flag_still_rejects_wrong_week_duration_snipe()
    {
        // Arrange — production Spotify finder previously set both flags true
        const string lastWeekYouTubeTitle =
            "Civic turnout strategies for mid-cycle ballot measures";
        const string thisWeekSpotifyTitle =
            "She Spent a Fortune in a Wellness Scheme with a Guest: New parenthood and a decade lost";
        var lastWeekYouTubeLength = TimeSpan.FromMinutes(59) + TimeSpan.FromSeconds(40);
        var thisWeekSpotifyLength = TimeSpan.FromMinutes(62) + TimeSpan.FromSeconds(39);
        var youTubeRelease = DomainTestFixture.UtcAtTime(-15, new TimeSpan(3, 30, 46));
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = lastWeekYouTubeTitle;
            e.Length = lastWeekYouTubeLength;
            e.Release = youTubeRelease;
            e.YouTubeId = _fixture.CreateYouTubeId();
        });
        var catalogueItem = _fixture.CreateEpisode(e =>
        {
            e.Title = thisWeekSpotifyTitle;
            e.Length = thisWeekSpotifyLength;
            e.Release = DomainTestFixture.SpotifyCatalogueReleaseDaysAfterYouTube(youTubeRelease, 2);
            e.SpotifyId = _fixture.CreateSpotifyId();
        });
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();

        // Act
        var result = _matcher.FindCatalogueMatchByLength(
            probe,
            [catalogueItem],
            podcast,
            episodeMatchRegex: null,
            new CatalogueMatchByLengthOptions(
                ReleaseAuthority: Service.YouTube,
                AcceptUniqueDurationWithoutTitleMatch: true,
                EnrichingYouTubeDiscoveredEpisode: true));

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When multiple catalogue rows share duration within the standard threshold, " +
        "FindCatalogueMatchByLength fuzzy-disambiguates by preferring the closest title match.")]
    public void multiple_same_length_candidates_fuzzy_prefers_closest_title()
    {
        // Arrange
        var sharedTitle = _fixture.CreateShortTitle();
        var probeLength = _fixture.CreateDuration();
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = sharedTitle;
            e.Length = probeLength;
            e.Release = DomainTestFixture.UtcDateDaysAgo(3);
        });
        var matchingCandidate = _fixture.CreateEpisode(e =>
        {
            e.Title = sharedTitle;
            e.Length = probeLength + TimeSpan.FromSeconds(10);
            e.Release = probe.Release;
            e.SpotifyId = _fixture.CreateSpotifyId();
        });
        var nonMatchingCandidate = _fixture.CreateEpisode(e =>
        {
            e.Title = _fixture.CreateTitle();
            e.Length = probeLength + TimeSpan.FromSeconds(20);
            e.Release = probe.Release;
            e.SpotifyId = _fixture.CreateSpotifyId();
        });
        var podcast = _fixture.CreatePodcast();

        // Act
        var result = _matcher.FindCatalogueMatchByLength(
            probe,
            [nonMatchingCandidate, matchingCandidate],
            podcast,
            episodeMatchRegex: null,
            new CatalogueMatchByLengthOptions());

        // Assert
        result.Should().BeSameAs(matchingCandidate);
    }

    [Fact(DisplayName =
        "When the probe episode has zero duration and no substring title overlap, " +
        "FindCatalogueMatchByLength returns no catalogue match.")]
    public void zero_length_probe_without_title_overlap_returns_null()
    {
        // Arrange
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = _fixture.CreateTitle();
            e.Length = TimeSpan.Zero;
            e.Release = DomainTestFixture.UtcDateDaysAgo(2);
        });
        var candidate = _fixture.CreateEpisode(e =>
        {
            e.Title = _fixture.CreateTitle();
            e.Length = _fixture.CreateDuration();
            e.Release = DomainTestFixture.UtcDateDaysAgo(2);
            e.SpotifyId = _fixture.CreateSpotifyId();
        });
        var podcast = _fixture.CreatePodcast();

        // Act
        var result = _matcher.FindCatalogueMatchByLength(
            probe,
            [candidate],
            podcast,
            episodeMatchRegex: null,
            new CatalogueMatchByLengthOptions());

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When the probe title contains HTML entities and the catalogue title is decoded, " +
        "FindCatalogueMatchByLength treats them as the same substring title match.")]
    public void html_entity_probe_title_matches_decoded_catalogue_title()
    {
        // Arrange
        var coreTitle = _fixture.CreateShortTitle();
        var decodedTitle = $"\"{coreTitle}\"";
        var encodedTitle = decodedTitle.Replace("\"", "&quot;", StringComparison.Ordinal);
        var sharedLength = _fixture.CreateDuration();
        var sharedRelease = DomainTestFixture.UtcDateDaysAgo(2);
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = encodedTitle;
            e.Length = sharedLength;
            e.Release = sharedRelease;
        });
        var matching = _fixture.CreateEpisode(e =>
        {
            e.Title = decodedTitle;
            e.Length = sharedLength;
            e.Release = sharedRelease;
            e.SpotifyId = _fixture.CreateSpotifyId();
        });
        var podcast = _fixture.CreatePodcast();

        // Act
        var result = _matcher.FindCatalogueMatchByLength(
            probe,
            [matching],
            podcast,
            episodeMatchRegex: null,
            new CatalogueMatchByLengthOptions());

        // Assert
        result.Should().BeSameAs(matching);
    }

    [Fact(DisplayName =
        "When a date-based catalogue lookup reducer excludes assigned platform IDs, " +
        "FindCatalogueMatchByDate does not return excluded candidates.")]
    public void date_lookup_reducer_excludes_assigned_platform_ids()
    {
        // Arrange
        var sharedTitle = _fixture.CreateTitle();
        var sharedRelease = DomainTestFixture.UtcDateDaysAgo(5);
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = sharedTitle;
            e.Release = sharedRelease;
        });
        var assignedId = _fixture.CreateSpotifyId();
        var availableId = _fixture.CreateSpotifyId();
        var excluded = _fixture.CreateEpisode(e =>
        {
            e.Title = sharedTitle;
            e.Release = sharedRelease;
            e.SpotifyId = assignedId;
        });
        var available = _fixture.CreateEpisode(e =>
        {
            e.Title = sharedTitle;
            e.Release = sharedRelease;
            e.SpotifyId = availableId;
        });
        var assignedIds = new HashSet<string> { assignedId };
        var podcast = _fixture.CreatePodcast();

        // Act
        var result = _matcher.FindCatalogueMatchByDate(
            probe,
            [excluded, available],
            podcast,
            episodeMatchRegex: null,
            e => string.IsNullOrWhiteSpace(e.SpotifyId) || !assignedIds.Contains(e.SpotifyId));

        // Assert
        result.Should().BeSameAs(available);
    }

    [Fact(DisplayName =
        "When probe and catalogue item share an exact title via IsMatch, merge accepts the pair " +
        "even when release and duration clearly differ.")]
    public void exact_title_is_match_merges_despite_mismatched_release_and_duration()
    {
        // KNOWN: likely wrong-merge risk — exact title short-circuits before release tolerance
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var sharedTitle = _fixture.CreateTitle();
        var storedLength = _fixture.CreateDuration();
        var incomingLength = storedLength + TimeSpan.FromMinutes(30);
        var storedRelease = DomainTestFixture.UtcDateDaysAgo(30);
        var incomingRelease = DomainTestFixture.UtcDateDaysAgo(2);
        var stored = _fixture.CreateEpisode(e =>
        {
            e.Title = sharedTitle;
            e.Length = storedLength;
            e.Release = storedRelease;
            e.SpotifyId = _fixture.CreateSpotifyId();
        });
        var incoming = _fixture.CreateEpisode(e =>
        {
            e.Title = sharedTitle;
            e.Length = incomingLength;
            e.Release = incomingRelease;
            e.AppleId = _fixture.CreateAppleId();
        });
        var expected = EpisodeExpectation.From(stored);

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [incoming]);

        // Assert
        result.MergedEpisodes.Should().ContainSingle();
        stored.AppleId.Should().Be(incoming.AppleId);
    }

    [Fact(DisplayName =
        "When an episode match regex is configured, exact full-title equality does not bypass " +
        "release and duration checks if the regex groups do not align.")]
    public void exact_title_bypass_does_not_apply_when_episode_match_regex_is_configured()
    {
        // Arrange
        const string episodeMatchRegex = @"#(?'episodematch'\d+)\s";
        var sharedTitle = _fixture.CreateTitle();
        var probeLength = _fixture.CreateDuration();
        var catalogueLength = probeLength + TimeSpan.FromMinutes(30);
        var probeRelease = DomainTestFixture.UtcDateDaysAgo(30);
        var catalogueRelease = DomainTestFixture.UtcDateDaysAgo(2);
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = sharedTitle;
            e.Length = probeLength;
            e.Release = probeRelease;
        });
        var catalogueItem = _fixture.CreateEpisode(e =>
        {
            e.Title = sharedTitle;
            e.Length = catalogueLength;
            e.Release = catalogueRelease;
            e.SpotifyId = _fixture.CreateSpotifyId();
        });
        var podcast = _fixture.CreatePodcast();
        podcast.EpisodeMatchRegex = episodeMatchRegex;

        // Act
        var matches = _matcher.IsCatalogueMatch(probe, catalogueItem, podcast, new System.Text.RegularExpressions.Regex(episodeMatchRegex));

        // Assert
        matches.Should().BeFalse();
    }

    [Theory(DisplayName =
        "Exact title match for IsCatalogueMatch is case-sensitive — differing case falls through " +
        "to fuzzy heuristics and does not auto-accept on title alone.")]
    [InlineData(true)]
    [InlineData(false)]
    public void exact_title_match_is_case_sensitive(bool alterCaseOnCatalogueTitle)
    {
        // Arrange
        var baseTitle = _fixture.CreateShortTitle();
        var probeTitle = baseTitle;
        var catalogueTitle = alterCaseOnCatalogueTitle
            ? new string(baseTitle.Select(c => char.IsLetter(c) ? char.ToUpperInvariant(c) : c).ToArray())
            : baseTitle;
        var probeLength = _fixture.CreateDuration();
        var probeRelease = DomainTestFixture.UtcDateDaysAgo(30);
        var catalogueRelease = DomainTestFixture.UtcDateDaysAgo(2);
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = probeTitle;
            e.Length = probeLength;
            e.Release = probeRelease;
        });
        var catalogueItem = _fixture.CreateEpisode(e =>
        {
            e.Title = catalogueTitle;
            e.Length = probeLength + TimeSpan.FromMinutes(30);
            e.Release = catalogueRelease;
            e.SpotifyId = _fixture.CreateSpotifyId();
        });
        var podcast = _fixture.CreatePodcast();

        // Act
        var matches = _matcher.IsCatalogueMatch(probe, catalogueItem, podcast, episodeMatchRegex: null);

        // Assert
        matches.Should().Be(!alterCaseOnCatalogueTitle);
    }

    [Fact(DisplayName =
        "Incident (Jul 2026): a YouTube-sourced probe whose release is delay-shifted to the expected " +
        "audio day must select the true 7-day-earlier audio counterpart (5s duration difference) rather " +
        "than the same-day-as-YouTube decoy (42s difference, released 48 minutes before the video).")]
    public void delay_shifted_youtube_probe_selects_true_counterpart_not_same_day_decoy()
    {
        // Arrange — daily show with a positive 7-day YouTube publishing delay; titles differ per platform.
        const string youTubeTitle = "The Mormon Church Called Me Chosen... and Cursed";
        const string decoyTitle = "I Was Groomed to Die for God";
        const string trueCounterpartTitle =
            "I Gave an Alien Cult $5 Million and 20 Years (Confessions of a Male Supermodel)";
        var publishingDelay = TimeSpan.FromDays(7);
        var videoLength = TimeSpan.FromSeconds(5017);
        var youTubePublish = DomainTestFixture.UtcAtTime(-1, new TimeSpan(13, 48, 0));
        var shiftedAnchor = youTubePublish - publishingDelay;
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = youTubeTitle;
            e.Length = videoLength;
            e.Release = shiftedAnchor;
        });
        var trueCounterpart = _fixture.CreateEpisode(e =>
        {
            e.Title = trueCounterpartTitle;
            e.Length = TimeSpan.FromSeconds(5012);
            e.Release = DomainTestFixture.SameCalendarDateWithTime(shiftedAnchor, new TimeSpan(13, 0, 0));
            e.AppleId = _fixture.CreateAppleId();
        });
        var sameDayAsYouTubeDecoy = _fixture.CreateEpisode(e =>
        {
            e.Title = decoyTitle;
            e.Length = TimeSpan.FromSeconds(4975);
            e.Release = DomainTestFixture.SameCalendarDateWithTime(youTubePublish, new TimeSpan(13, 0, 0));
            e.AppleId = _fixture.CreateAppleId();
        });
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.AppleId = _fixture.CreateAppleId();
            p.YouTubePublicationOffset = publishingDelay.Ticks;
        });

        // Act — isolates the corrected release anchor. Factory tests separately verify that
        // YouTube-sourced criteria both produce this anchor and enable YouTube enrichment.
        var result = _matcher.FindCatalogueMatchByLength(
            probe,
            [sameDayAsYouTubeDecoy, trueCounterpart],
            podcast,
            episodeMatchRegex: null,
            new CatalogueMatchByLengthOptions());

        // Assert
        result.Should().NotBeSameAs(sameDayAsYouTubeDecoy);
        result.Should().BeSameAs(trueCounterpart);
    }

    [Fact(DisplayName =
        "The last-resort same-release-window fallback must not accept a candidate whose duration " +
        "differs from the probe beyond the broader threshold on a weak fuzzy-title score alone.")]
    public void same_release_window_fallback_rejects_duration_mismatch_beyond_broader_threshold()
    {
        // Arrange — same-day audio decoy within 3h of the probe release but >90s off in duration;
        // titles score just above the weak fallback threshold (real incident titles).
        const string youTubeTitle = "The Mormon Church Called Me Chosen... and Cursed";
        const string decoyTitle = "I Was Groomed to Die for God";
        var probeRelease = DomainTestFixture.UtcAtTime(-1, new TimeSpan(13, 48, 0));
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = youTubeTitle;
            e.Length = TimeSpan.FromSeconds(5017);
            e.Release = probeRelease;
        });
        var sameDayDecoy = _fixture.CreateEpisode(e =>
        {
            e.Title = decoyTitle;
            e.Length = TimeSpan.FromSeconds(5017 - 200);
            e.Release = probeRelease.AddMinutes(-48);
            e.AppleId = _fixture.CreateAppleId();
        });
        var podcast = _fixture.CreatePodcast(p => p.AppleId = _fixture.CreateAppleId());

        // Act
        var result = _matcher.FindCatalogueMatchByLength(
            probe,
            [sameDayDecoy],
            podcast,
            episodeMatchRegex: null,
            new CatalogueMatchByLengthOptions());

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When the probe episode has no release date, " +
        "FindCatalogueMatchByDate returns no catalogue match.")]
    public void date_match_returns_null_when_probe_has_no_release()
    {
        // Arrange
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = _fixture.CreateTitle();
            e.Release = DateTime.MinValue;
        });
        var candidate = _fixture.CreateEpisode(e =>
        {
            e.Title = _fixture.CreateTitle();
            e.Release = DomainTestFixture.UtcDateDaysAgo(3);
            e.SpotifyId = _fixture.CreateSpotifyId();
        });
        var podcast = _fixture.CreatePodcast();

        // Act
        var result = _matcher.FindCatalogueMatchByDate(
            probe,
            [candidate],
            podcast,
            episodeMatchRegex: null);

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When exactly one catalogue row shares the probe duration within the standard threshold " +
        "and titles fuzzy-match at the catalogue threshold, FindCatalogueMatchByLength selects it.")]
    public void single_duration_candidate_fuzzy_match_at_catalogue_threshold()
    {
        // Arrange
        var storedTitle = _fixture.CreateShortTitle();
        var incomingTitle = DomainTestFixture.CreateTypoTitleVariant(storedTitle);
        var probeLength = _fixture.CreateDuration();
        var catalogueLength = probeLength + TimeSpan.FromSeconds(20);
        var sharedRelease = DomainTestFixture.UtcDateDaysAgo(2);
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
            new CatalogueMatchByLengthOptions());

        // Assert
        result.Should().BeSameAs(matchingCandidate);
    }

    [Fact(DisplayName =
        "When enriching a YouTube-discovered episode and multiple catalogue rows align on release " +
        "within twelve hours but all sit outside the duration band, FindCatalogueMatchByLength returns null " +
        "because multi-criteria scoring requires duration within band.")]
    public void youtube_discovered_multiple_release_only_candidates_outside_duration_band_returns_null()
    {
        // Arrange - typo titles avoid early containment; durations far outside proportional band
        var sharedTitle = _fixture.CreateShortTitle();
        var probeLength = TimeSpan.FromMinutes(60);
        var probeRelease = DomainTestFixture.UtcAtTime(-4, _fixture.CreateNonMidnightTimeOfDay());
        var closerRelease = probeRelease.AddHours(4);
        var fartherRelease = probeRelease.AddHours(10);
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = sharedTitle;
            e.Length = probeLength;
            e.Release = probeRelease;
            e.Subjects = [];
        });
        var closerCandidate = _fixture.CreateEpisode(e =>
        {
            e.Title = DomainTestFixture.CreateTypoTitleVariant(sharedTitle);
            e.Length = TimeSpan.FromMinutes(30);
            e.Release = closerRelease;
            e.AppleId = _fixture.CreateAppleId();
            e.Subjects = [];
        });
        var fartherCandidate = _fixture.CreateEpisode(e =>
        {
            e.Title = DomainTestFixture.CreateTypoTitleVariant(sharedTitle);
            e.Length = TimeSpan.FromMinutes(35);
            e.Release = fartherRelease;
            e.AppleId = _fixture.CreateAppleId();
            e.Subjects = [];
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
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When multiple catalogue rows share the probe calendar date, " +
        "FindCatalogueMatchByDate fuzzy-disambiguates by preferring the closest title match.")]
    public void date_match_with_multiple_same_date_candidates_fuzzy_disambiguates()
    {
        // Arrange
        var sharedTitle = _fixture.CreateShortTitle();
        var typoTitle = DomainTestFixture.CreateTypoTitleVariant(sharedTitle);
        var unrelatedTitle = _fixture.CreateTitle();
        var sharedRelease = DomainTestFixture.UtcDateDaysAgo(7);
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = sharedTitle;
            e.Release = sharedRelease;
        });
        var matching = _fixture.CreateEpisode(e =>
        {
            e.Title = typoTitle;
            e.Release = sharedRelease;
            e.SpotifyId = _fixture.CreateSpotifyId();
        });
        var nonMatching = _fixture.CreateEpisode(e =>
        {
            e.Title = unrelatedTitle;
            e.Release = sharedRelease;
            e.SpotifyId = _fixture.CreateSpotifyId();
        });
        var podcast = _fixture.CreatePodcast();

        // Act
        var result = _matcher.FindCatalogueMatchByDate(
            probe,
            [nonMatching, matching],
            podcast,
            episodeMatchRegex: null);

        // Assert
        result.Should().BeSameAs(matching);
    }

    [Fact(DisplayName =
        "When a catalogue row has no release date, " +
        "FindCatalogueMatchByDate may still include it in the same-date candidate set.")]
    public void date_match_includes_catalogue_row_with_min_release_when_probe_has_release()
    {
        // Arrange
        var sharedTitle = _fixture.CreateTitle();
        var probeRelease = DomainTestFixture.UtcDateDaysAgo(5);
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = sharedTitle;
            e.Release = probeRelease;
        });
        var candidateWithoutRelease = _fixture.CreateEpisode(e =>
        {
            e.Title = sharedTitle;
            e.Release = DateTime.MinValue;
            e.SpotifyId = _fixture.CreateSpotifyId();
        });
        var podcast = _fixture.CreatePodcast();

        // Act
        var result = _matcher.FindCatalogueMatchByDate(
            probe,
            [candidateWithoutRelease],
            podcast,
            episodeMatchRegex: null);

        // Assert
        result.Should().BeSameAs(candidateWithoutRelease);
    }


    [Fact(DisplayName =
        "Incident (Jul 2026 / Aug 2026): when enriching a YouTube-discovered episode, title containment " +
        "(editorial prefix + case variation) and duration within five minutes still fail when the date-only " +
        "Spotify release sits two calendar days before the probe - multi-criteria scoring requires the " +
        "Spotify catalogue release window before title points can contribute.")]
    public void youtube_discovered_title_confident_duration_outside_spotify_release_window_returns_null()
    {
        // Arrange - probe body carries capitals; Spotify row is "<prefix> " + lowercased probe body,
        // so the case-sensitive early containment gate fails and matching falls to CatalogueMatchScorer.
        var probeTitle = "Nightshade: How The Victims Were Silenced";
        var spotifyTitle = "Special Report " + probeTitle.ToLowerInvariant();
        var probeLength = new TimeSpan(0, 25, 19);
        var spotifyLength = new TimeSpan(0, 27, 58);
        var probeRelease = DomainTestFixture.UtcAtTime(-2, new TimeSpan(9, 7, 30));
        var spotifyRelease = probeRelease.Date.AddDays(-2);
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = probeTitle;
            e.Length = probeLength;
            e.Release = probeRelease;
            e.YouTubeId = _fixture.CreateYouTubeId();
            e.Subjects = [];
        });
        var spotifyCandidate = _fixture.CreateEpisode(e =>
        {
            e.Title = spotifyTitle;
            e.Length = spotifyLength;
            e.Release = spotifyRelease;
            e.SpotifyId = _fixture.CreateSpotifyId();
            e.Subjects = [];
        });
        var podcast = _fixture.CreatePodcast(p =>
            p.YouTubePublicationOffset = TimeSpan.FromHours(1).Ticks);

        // Act
        var result = _matcher.FindCatalogueMatchByLength(
            probe,
            [spotifyCandidate],
            podcast,
            episodeMatchRegex: null,
            new CatalogueMatchByLengthOptions(EnrichingYouTubeDiscoveredEpisode: true));

        // Assert
        result.Should().BeNull();
    }
}
