using FluentAssertions;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Matching;

/// <summary>
/// Multi-criteria catalogue match scoring for YouTube-discovered Spotify/Apple enrich.
/// </summary>
public class CatalogueMatchScorerRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Duration within band plus same-calendar-day release alone scores below the match threshold, " +
        "because Aug 2026 wrong-attach protection forbids duration+day-only accepts.")]
    public void duration_and_same_day_alone_fails_threshold()
    {
        // Arrange
        const string probeTitle =
            "Guest Answers Live Questions About A Political Figure And An Identity Foundation";
        const string catalogueTitle =
            "A Decade Inside An Arranged Marriage And The Exit That Followed";
        var length = _fixture.CreateDuration();
        var release = DomainTestFixture.UtcDateDaysAgo(1);
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = probeTitle;
            e.Description = string.Empty;
            e.Length = length;
            e.Release = release;
            e.YouTubeId = _fixture.CreateYouTubeId();
            e.Subjects = [];
        });
        var catalogue = _fixture.CreateEpisode(e =>
        {
            e.Title = catalogueTitle;
            e.Description = string.Empty;
            e.Length = length;
            e.Release = release;
            e.SpotifyId = _fixture.CreateSpotifyId();
            e.Subjects = [];
        });

        // Act
        var score = CatalogueMatchScorer.Score(probe, catalogue);

        // Assert
        score.Should().Be(
            CatalogueMatchScorer.DurationWithinBandPoints +
            CatalogueMatchScorer.SameCalendarDayReleasePoints);
        score.Should().BeLessThan(CatalogueMatchScorer.MatchThreshold);
        CatalogueMatchScorer.MeetsMatchThreshold(probe, catalogue).Should().BeFalse();
    }

    [Fact(DisplayName =
        "Duration, same-day release, and one shared classified subject meet the match threshold, " +
        "so editorial renames that keep a proper noun can still match.")]
    public void duration_same_day_and_one_shared_subject_meets_threshold()
    {
        // Arrange
        var length = _fixture.CreateDuration();
        var release = DomainTestFixture.UtcDateDaysAgo(1);
        var sharedSubject = _fixture.CreateTitle(3);
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = _fixture.CreateTitle();
            e.Length = length;
            e.Release = release;
            e.YouTubeId = _fixture.CreateYouTubeId();
            e.Subjects = [sharedSubject];
        });
        var catalogue = _fixture.CreateEpisode(e =>
        {
            e.Title = _fixture.CreateTitle();
            e.Length = length;
            e.Release = release;
            e.SpotifyId = _fixture.CreateSpotifyId();
            e.Subjects = [sharedSubject];
        });

        // Act
        var score = CatalogueMatchScorer.Score(probe, catalogue);

        // Assert
        score.Should().Be(
            CatalogueMatchScorer.DurationWithinBandPoints +
            CatalogueMatchScorer.SameCalendarDayReleasePoints +
            CatalogueMatchScorer.SingleSharedSubjectPoints);
        CatalogueMatchScorer.MeetsMatchThreshold(probe, catalogue).Should().BeTrue();
    }

    [Fact(DisplayName =
        "Two shared classified subjects score higher than one shared subject on the subject vector.")]
    public void two_shared_subjects_score_higher_than_one()
    {
        // Arrange
        var length = _fixture.CreateDuration();
        var release = DomainTestFixture.UtcDateDaysAgo(2);
        var subjectA = _fixture.CreateTitle(3);
        var subjectB = _fixture.CreateTitle(3);
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = _fixture.CreateTitle();
            e.Length = length;
            e.Release = release;
            e.Subjects = [subjectA, subjectB];
        });
        var oneShared = _fixture.CreateEpisode(e =>
        {
            e.Title = _fixture.CreateTitle();
            e.Length = length;
            e.Release = release;
            e.SpotifyId = _fixture.CreateSpotifyId();
            e.Subjects = [subjectA];
        });
        var twoShared = _fixture.CreateEpisode(e =>
        {
            e.Title = _fixture.CreateTitle();
            e.Length = length;
            e.Release = release;
            e.SpotifyId = _fixture.CreateSpotifyId();
            e.Subjects = [subjectA, subjectB];
        });

        // Act
        var oneScore = CatalogueMatchScorer.Score(probe, oneShared);
        var twoScore = CatalogueMatchScorer.Score(probe, twoShared);

        // Assert
        twoScore.Should().Be(oneScore - CatalogueMatchScorer.SingleSharedSubjectPoints +
                             CatalogueMatchScorer.MultipleSharedSubjectPoints);
        twoScore.Should().BeGreaterThan(oneScore);
    }

    [Fact(DisplayName =
        "Podcast default subject alone does not contribute subject points when supplied as DefaultSubject filter.")]
    public void default_subject_overlap_is_ignored()
    {
        // Arrange
        var length = _fixture.CreateDuration();
        var release = DomainTestFixture.UtcDateDaysAgo(1);
        var defaultSubject = _fixture.CreateTitle(2);
        var filters = new CatalogueSubjectScoreFilters(DefaultSubject: defaultSubject);
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = _fixture.CreateTitle();
            e.Length = length;
            e.Release = release;
            e.Subjects = [defaultSubject];
        });
        var catalogue = _fixture.CreateEpisode(e =>
        {
            e.Title = _fixture.CreateTitle();
            e.Length = length;
            e.Release = release;
            e.SpotifyId = _fixture.CreateSpotifyId();
            e.Subjects = [defaultSubject];
        });

        // Act
        var score = CatalogueMatchScorer.Score(probe, catalogue, filters);

        // Assert
        score.Should().Be(
            CatalogueMatchScorer.DurationWithinBandPoints +
            CatalogueMatchScorer.SameCalendarDayReleasePoints);
        CatalogueMatchScorer.MeetsMatchThreshold(probe, catalogue, filters).Should().BeFalse();
    }

    [Fact(DisplayName =
        "SelectBestMatch returns the highest-scoring candidate that meets the threshold and null when none do.")]
    public void select_best_match_picks_highest_scoring_threshold_candidate()
    {
        // Arrange
        var length = _fixture.CreateDuration();
        var release = DomainTestFixture.UtcDateDaysAgo(1);
        var sharedSubject = _fixture.CreateTitle(3);
        var youTubeTitle = _fixture.CreateTitle(4);
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = youTubeTitle;
            e.Description = string.Empty;
            e.Length = length;
            e.Release = release;
            e.YouTubeId = _fixture.CreateYouTubeId();
            e.Subjects = [sharedSubject];
        });
        var weak = _fixture.CreateEpisode(e =>
        {
            // Disjoint title + empty description so duration+day alone stay below threshold
            e.Title =
                "Guest Answers Live Questions About A Political Figure And An Identity Foundation";
            e.Description = string.Empty;
            e.Length = length;
            e.Release = release;
            e.SpotifyId = _fixture.CreateSpotifyId();
            e.Subjects = [];
        });
        var strong = _fixture.CreateEpisode(e =>
        {
            e.Title = $"{youTubeTitle}: editorial rename";
            e.Description = string.Empty;
            e.Length = length;
            e.Release = release;
            e.SpotifyId = _fixture.CreateSpotifyId();
            e.Subjects = [sharedSubject];
        });

        // Act
        var best = CatalogueMatchScorer.SelectBestMatch(probe, [weak, strong]);
        var none = CatalogueMatchScorer.SelectBestMatch(probe, [weak]);

        // Assert
        best.Should().BeSameAs(strong);
        none.Should().BeNull();
    }

    [Fact(DisplayName =
        "An Apple catalogue row released in the early-morning audio slot matches a YouTube probe " +
        "published later the same calendar day, even though the gap exceeds twelve hours.")]
    public void apple_same_calendar_day_beyond_twelve_hours_meets_threshold()
    {
        // Arrange
        var baseTitle = _fixture.CreateTitle(5);
        var youTubeRelease = DomainTestFixture.UtcAtTime(-1, TimeSpan.FromHours(17) + TimeSpan.FromMinutes(15));
        var appleRelease = DomainTestFixture.UtcAtTime(-1, TimeSpan.FromMinutes(5));
        var length = _fixture.CreateDuration();
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = baseTitle;
            e.Description = string.Empty;
            e.Length = length;
            e.Release = youTubeRelease;
            e.YouTubeId = _fixture.CreateYouTubeId();
            e.Subjects = [];
        });
        var catalogue = _fixture.CreateEpisode(e =>
        {
            e.Title = DomainTestFixture.CreateTypoTitleVariant(baseTitle);
            e.Description = string.Empty;
            e.Length = length;
            e.Release = appleRelease;
            e.AppleId = _fixture.CreateAppleId();
            e.Subjects = [];
        });

        // Act
        var score = CatalogueMatchScorer.Score(probe, catalogue);

        // Assert
        (youTubeRelease - appleRelease).Should().BeGreaterThan(TimeSpan.FromHours(12));
        score.Should().Be(
            CatalogueMatchScorer.DurationWithinBandPoints +
            CatalogueMatchScorer.SameCalendarDayReleasePoints +
            CatalogueMatchScorer.FuzzyTitlePoints);
        CatalogueMatchScorer.MeetsMatchThreshold(probe, catalogue).Should().BeTrue();
    }

    [Fact(DisplayName =
        "An Apple catalogue row released more than a day either side of the probe scores zero, " +
        "so widening to calendar-day tolerance does not admit wrong-week audio.")]
    public void apple_outside_day_tolerance_scores_zero()
    {
        // Arrange
        var baseTitle = _fixture.CreateTitle(5);
        var length = _fixture.CreateDuration();
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = baseTitle;
            e.Description = string.Empty;
            e.Length = length;
            e.Release = DomainTestFixture.UtcAtTime(-1, TimeSpan.FromHours(17));
            e.YouTubeId = _fixture.CreateYouTubeId();
            e.Subjects = [];
        });
        var catalogue = _fixture.CreateEpisode(e =>
        {
            e.Title = baseTitle;
            e.Description = string.Empty;
            e.Length = length;
            e.Release = DomainTestFixture.UtcAtTime(-4, TimeSpan.FromHours(17));
            e.AppleId = _fixture.CreateAppleId();
            e.Subjects = [];
        });

        // Act
        var score = CatalogueMatchScorer.Score(probe, catalogue);

        // Assert
        score.Should().Be(0);
    }

    [Fact(DisplayName =
        "When the catalogue row has no duration (Apple omitting durationInMilliseconds), " +
        "same-calendar-day release and disjoint titles with empty subjects stay below the threshold, " +
        "because missing duration must not revive release-only attaches.")]
    public void missing_catalogue_duration_same_day_disjoint_titles_fails_threshold()
    {
        // Arrange
        const string probeTitle =
            "Guest Answers Live Questions About A Political Figure And An Identity Foundation";
        const string catalogueTitle =
            "A Decade Inside An Arranged Marriage And The Exit That Followed";
        var release = DomainTestFixture.UtcDateDaysAgo(1);
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = probeTitle;
            e.Description = string.Empty;
            e.Length = _fixture.CreateDuration();
            e.Release = release;
            e.YouTubeId = _fixture.CreateYouTubeId();
            e.Subjects = [];
        });
        var catalogue = _fixture.CreateEpisode(e =>
        {
            e.Title = catalogueTitle;
            e.Description = string.Empty;
            e.Length = TimeSpan.Zero;
            e.Release = release;
            e.AppleId = _fixture.CreateAppleId();
            e.Subjects = [];
        });

        // Act
        var score = CatalogueMatchScorer.Score(probe, catalogue);

        // Assert
        score.Should().Be(CatalogueMatchScorer.SameCalendarDayReleasePoints);
        CatalogueMatchScorer.MeetsMatchThreshold(probe, catalogue).Should().BeFalse();
    }

    [Fact(DisplayName =
        "When the catalogue row has no duration, same-day release plus fuzzy title and one shared " +
        "classified subject meet the match threshold, because title and subjects replace duration evidence.")]
    public void missing_catalogue_duration_same_day_fuzzy_title_and_subject_meets_threshold()
    {
        // Arrange
        var release = DomainTestFixture.UtcDateDaysAgo(1);
        var sharedSubject = _fixture.CreateTitle(3);
        var baseTitle = _fixture.CreateTitle(5);
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = baseTitle;
            e.Length = _fixture.CreateDuration();
            e.Release = release;
            e.YouTubeId = _fixture.CreateYouTubeId();
            e.Subjects = [sharedSubject];
        });
        var catalogue = _fixture.CreateEpisode(e =>
        {
            e.Title = DomainTestFixture.CreateTypoTitleVariant(baseTitle);
            e.Length = TimeSpan.Zero;
            e.Release = release;
            e.AppleId = _fixture.CreateAppleId();
            e.Subjects = [sharedSubject];
        });

        // Act
        var score = CatalogueMatchScorer.Score(probe, catalogue);

        // Assert
        score.Should().Be(
            CatalogueMatchScorer.SameCalendarDayReleasePoints +
            CatalogueMatchScorer.FuzzyTitlePoints +
            CatalogueMatchScorer.SingleSharedSubjectPoints);
        CatalogueMatchScorer.MeetsMatchThreshold(probe, catalogue).Should().BeTrue();
    }

    [Fact(DisplayName =
        "For a long YouTube-discovered episode, an Apple catalogue row a little over five minutes longer " +
        "still awards duration points and meets the threshold with same-day release and a fuzzy title, " +
        "because the duration band is max(five minutes, ten percent of the shorter length).")]
    public void long_episode_apple_slightly_longer_than_five_minutes_meets_threshold()
    {
        // Arrange — ~97m YouTube vs ~102.5m Apple (~5.5m gap); 10% of shorter ≈ 9.7m
        var release = DomainTestFixture.UtcDateDaysAgo(1);
        var baseTitle = _fixture.CreateTitle(6);
        var youTubeLength = TimeSpan.FromMinutes(97);
        var appleLength = youTubeLength + TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(30);
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = baseTitle;
            e.Description = string.Empty;
            e.Length = youTubeLength;
            e.Release = release;
            e.YouTubeId = _fixture.CreateYouTubeId();
            e.Subjects = [];
        });
        var catalogue = _fixture.CreateEpisode(e =>
        {
            e.Title = DomainTestFixture.CreateTypoTitleVariant(baseTitle);
            e.Description = string.Empty;
            e.Length = appleLength;
            e.Release = release;
            e.AppleId = _fixture.CreateAppleId();
            e.Subjects = [];
        });

        // Act
        var score = CatalogueMatchScorer.Score(probe, catalogue);
        var band = CatalogueMatchScorer.GetDurationBand(youTubeLength, appleLength);

        // Assert
        (appleLength - youTubeLength).Should().BeGreaterThan(CatalogueMatchScorer.DurationBandFloor);
        (appleLength - youTubeLength).Should().BeLessThan(band);
        score.Should().Be(
            CatalogueMatchScorer.DurationWithinBandPoints +
            CatalogueMatchScorer.SameCalendarDayReleasePoints +
            CatalogueMatchScorer.FuzzyTitlePoints);
        CatalogueMatchScorer.MeetsMatchThreshold(probe, catalogue).Should().BeTrue();
    }

    [Fact(DisplayName =
        "When both sides have duration and the gap equals the proportional band for a long episode, " +
        "the score is zero because the duration hard-fail uses a strict less-than band edge.")]
    public void long_episode_duration_gap_exactly_at_proportional_band_scores_zero()
    {
        // Arrange
        var release = DomainTestFixture.UtcDateDaysAgo(1);
        var baseTitle = _fixture.CreateTitle(6);
        var shorter = TimeSpan.FromMinutes(100);
        var band = CatalogueMatchScorer.GetDurationBand(shorter, shorter);
        var longer = shorter + band;
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = baseTitle;
            e.Description = string.Empty;
            e.Length = shorter;
            e.Release = release;
            e.YouTubeId = _fixture.CreateYouTubeId();
            e.Subjects = [];
        });
        var catalogue = _fixture.CreateEpisode(e =>
        {
            e.Title = DomainTestFixture.CreateTypoTitleVariant(baseTitle);
            e.Description = string.Empty;
            e.Length = longer;
            e.Release = release;
            e.AppleId = _fixture.CreateAppleId();
            e.Subjects = [];
        });

        // Act
        var score = CatalogueMatchScorer.Score(probe, catalogue);

        // Assert
        band.Should().BeGreaterThan(CatalogueMatchScorer.DurationBandFloor);
        score.Should().Be(0);
    }

    [Fact(DisplayName =
        "When both sides have duration and the gap is just inside the proportional band for a long episode, " +
        "duration points are awarded so same-day release plus fuzzy title can meet the threshold.")]
    public void long_episode_duration_gap_just_inside_proportional_band_meets_threshold()
    {
        // Arrange
        var release = DomainTestFixture.UtcDateDaysAgo(1);
        var baseTitle = _fixture.CreateTitle(6);
        var shorter = TimeSpan.FromMinutes(100);
        var band = CatalogueMatchScorer.GetDurationBand(shorter, shorter);
        var longer = shorter + band - TimeSpan.FromTicks(1);
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = baseTitle;
            e.Description = string.Empty;
            e.Length = shorter;
            e.Release = release;
            e.YouTubeId = _fixture.CreateYouTubeId();
            e.Subjects = [];
        });
        var catalogue = _fixture.CreateEpisode(e =>
        {
            e.Title = DomainTestFixture.CreateTypoTitleVariant(baseTitle);
            e.Description = string.Empty;
            e.Length = longer;
            e.Release = release;
            e.AppleId = _fixture.CreateAppleId();
            e.Subjects = [];
        });

        // Act
        var score = CatalogueMatchScorer.Score(probe, catalogue);

        // Assert
        band.Should().BeGreaterThan(CatalogueMatchScorer.DurationBandFloor);
        score.Should().Be(
            CatalogueMatchScorer.DurationWithinBandPoints +
            CatalogueMatchScorer.SameCalendarDayReleasePoints +
            CatalogueMatchScorer.FuzzyTitlePoints);
        CatalogueMatchScorer.MeetsMatchThreshold(probe, catalogue).Should().BeTrue();
    }

    [Fact(DisplayName =
        "For a short episode the duration band stays at the five-minute floor, so a gap of exactly " +
        "five minutes still hard-fails even though ten percent of the shorter length is smaller.")]
    public void short_episode_duration_band_keeps_five_minute_floor()
    {
        // Arrange — 20m → 10% = 2m; floor remains 5m
        var release = DomainTestFixture.UtcDateDaysAgo(1);
        var baseTitle = _fixture.CreateTitle(6);
        var shorter = TimeSpan.FromMinutes(20);
        var longer = shorter + CatalogueMatchScorer.DurationBandFloor;
        var probe = _fixture.CreateEpisode(e =>
        {
            e.Title = baseTitle;
            e.Description = string.Empty;
            e.Length = shorter;
            e.Release = release;
            e.YouTubeId = _fixture.CreateYouTubeId();
            e.Subjects = [];
        });
        var catalogue = _fixture.CreateEpisode(e =>
        {
            e.Title = DomainTestFixture.CreateTypoTitleVariant(baseTitle);
            e.Description = string.Empty;
            e.Length = longer;
            e.Release = release;
            e.AppleId = _fixture.CreateAppleId();
            e.Subjects = [];
        });

        // Act
        var band = CatalogueMatchScorer.GetDurationBand(shorter, longer);
        var score = CatalogueMatchScorer.Score(probe, catalogue);

        // Assert
        band.Should().Be(CatalogueMatchScorer.DurationBandFloor);
        score.Should().Be(0);
    }
}
