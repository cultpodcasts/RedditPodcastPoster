using FluentAssertions;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Matching;

/// <summary>
/// Unit coverage for <see cref="CrossPlatformMatchScorer"/> signal tiers and threshold 60.
/// </summary>
public class CrossPlatformMatchScorerRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Delay-aligned release plus duration scores 70 and meets the match threshold without title confidence.")]
    public void Delay_aligned_release_and_duration_meets_threshold_without_title()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var (youTube, audio) = CreateDelayAlignedDivergentPair(podcast);

        // Act
        var score = CrossPlatformMatchScorer.Score(youTube, audio, podcast);

        // Assert
        score.Should().Be(
            CrossPlatformMatchScorer.DurationWithinBandPoints +
            CrossPlatformMatchScorer.DelayAlignedReleasePoints);
        CrossPlatformMatchScorer.MeetsMatchThreshold(youTube, audio, podcast).Should().BeTrue();
    }

    [Fact(DisplayName =
        "Same-calendar-day release plus duration scores exactly 60 and meets the match threshold without title.")]
    public void Same_calendar_day_release_and_duration_meets_threshold_at_boundary()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var youTubeRelease = DomainTestFixture.UtcAtTime(-10, TimeSpan.FromHours(15));
        var audioRelease = youTubeRelease.Date.Add(TimeSpan.FromHours(8));
        var length = TimeSpan.FromMinutes(60);
        var (youTube, audio) = CreateYouTubeAudioPair(
            podcast,
            youTubeRelease,
            audioRelease,
            length,
            "Alpha market briefing on early catalogue drift signals",
            "Omega wellness interview about unrelated guest journeys");

        EpisodeReleaseTolerance.IsYouTubePublishDelayAligned(
                audioRelease, youTubeRelease, podcast.YouTubePublishingDelay())
            .Should().BeFalse("fixture must not accidentally delay-align");
        EpisodeReleaseTolerance.AreCrossPlatformReleasesOnSameCalendarDay(audioRelease, youTubeRelease)
            .Should().BeTrue();

        // Act
        var score = CrossPlatformMatchScorer.Score(youTube, audio, podcast);

        // Assert
        score.Should().Be(CrossPlatformMatchScorer.MatchThreshold);
        CrossPlatformMatchScorer.MeetsMatchThreshold(youTube, audio, podcast).Should().BeTrue();
    }

    [Fact(DisplayName =
        "Weak catalogue-day release plus duration scores 45 and stays below the match threshold without title.")]
    public void Weak_catalogue_release_and_duration_stays_below_threshold()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var (youTube, audio) = _fixture.CreateNegativeDelayNonMatchingPair(podcast);

        // Act
        var score = CrossPlatformMatchScorer.Score(youTube, audio, podcast);

        // Assert
        score.Should().Be(
            CrossPlatformMatchScorer.DurationWithinBandPoints +
            CrossPlatformMatchScorer.WeakCatalogueReleasePoints);
        // Assert
        score.Should().BeLessThan(CrossPlatformMatchScorer.MatchThreshold);
        CrossPlatformMatchScorer.MeetsMatchThreshold(youTube, audio, podcast).Should().BeFalse();
    }

    [Fact(DisplayName =
        "Early-within-negative-delay release plus duration plus matching descriptions reaches 70 and " +
        "meets the threshold even when marketing titles are wholly disjoint.")]
    public void Early_within_delay_with_matching_descriptions_meets_threshold()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var (youTube, audio, _) =
            _fixture.CreateYouTubeAuthorityNegativeOffsetEarlyAudioPair(podcast, matchingTitles: false);

        // Act
        var score = CrossPlatformMatchScorer.Score(youTube, audio, podcast);

        // Assert
        score.Should().Be(
            CrossPlatformMatchScorer.DurationWithinBandPoints +
            CrossPlatformMatchScorer.WeakCatalogueReleasePoints +
            CrossPlatformMatchScorer.FuzzyDescriptionPoints);
        CrossPlatformMatchScorer.MeetsMatchThreshold(youTube, audio, podcast).Should().BeTrue();
    }

    [Fact(DisplayName =
        "Weak catalogue-day release plus duration plus fuzzy title reaches 70 and meets the match threshold.")]
    public void Weak_catalogue_release_with_fuzzy_title_meets_threshold()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var delay = podcast.YouTubePublishingDelay();
        var youTubeRelease = DomainTestFixture.UtcAtTime(-40, TimeSpan.FromHours(15));
        var audioRelease = (youTubeRelease - delay).Date.AddDays(5);
        var length = TimeSpan.FromMinutes(55);
        const string baseTitle = "Holy Disobedience Inside the Seventh-day Adventist Church";
        var (youTube, audio) = CreateYouTubeAudioPair(
            podcast,
            youTubeRelease,
            audioRelease,
            length,
            baseTitle,
            baseTitle + " with Melissa Duge Spiers");

        // Act
        var score = CrossPlatformMatchScorer.Score(youTube, audio, podcast);

        // Assert
        score.Should().Be(
            CrossPlatformMatchScorer.DurationWithinBandPoints +
            CrossPlatformMatchScorer.WeakCatalogueReleasePoints +
            CrossPlatformMatchScorer.FuzzyTitlePoints);
        CrossPlatformMatchScorer.MeetsMatchThreshold(youTube, audio, podcast).Should().BeTrue();
    }

    [Fact(DisplayName =
        "Weak catalogue-day release plus duration plus a substring/fuzzy title relationship reaches at least " +
        "65 and meets the match threshold (title points push past the 45 weak-release floor).")]
    public void Weak_catalogue_release_with_substring_title_meets_threshold()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var delay = podcast.YouTubePublishingDelay();
        var youTubeRelease = DomainTestFixture.UtcAtTime(-40, TimeSpan.FromHours(15));
        var audioRelease = (youTubeRelease - delay).Date.AddDays(5);
        var length = TimeSpan.FromMinutes(55);
        // Short core contained in a longer title: substring relationship; may also fuzzy-match.
        const string core = "zkq-match-core-token";
        var (youTube, audio) = CreateYouTubeAudioPair(
            podcast,
            youTubeRelease,
            audioRelease,
            length,
            core,
            "Broadcast archive " + core + " extended cut");

        // Act
        var score = CrossPlatformMatchScorer.Score(youTube, audio, podcast);
        var weakFloor =
            CrossPlatformMatchScorer.DurationWithinBandPoints +
            CrossPlatformMatchScorer.WeakCatalogueReleasePoints;

        // Assert
        score.Should().BeOneOf(
            weakFloor + CrossPlatformMatchScorer.SubstringTitlePoints,
            weakFloor + CrossPlatformMatchScorer.FuzzyTitlePoints);
        // Assert
        score.Should().BeGreaterThanOrEqualTo(CrossPlatformMatchScorer.MatchThreshold);
        CrossPlatformMatchScorer.MeetsMatchThreshold(youTube, audio, podcast).Should().BeTrue();
    }

    [Fact(DisplayName =
        "Delay-aligned release takes precedence over same-calendar-day via max of delay-proximity and " +
        "calendar-day points — tiers do not stack.")]
    public void Delay_aligned_release_tier_does_not_stack_same_calendar_day_points()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        podcast.YouTubePublicationOffset = TimeSpan.FromHours(-8).Ticks;
        var audioRelease = DomainTestFixture.UtcAtTime(-20, TimeSpan.FromHours(8));
        var youTubeRelease = audioRelease.Add(podcast.YouTubePublishingDelay());
        youTubeRelease.Date.Should().Be(audioRelease.Date, "same calendar day while delay-aligned");
        var length = TimeSpan.FromMinutes(62);
        var (youTube, audio) = CreateYouTubeAudioPair(
            podcast,
            youTubeRelease,
            audioRelease,
            length,
            "Alpha market briefing on early catalogue drift signals",
            "Omega wellness interview about unrelated guest journeys");

        // Act
        var score = CrossPlatformMatchScorer.Score(youTube, audio, podcast);

        // Assert
        score.Should().Be(
            CrossPlatformMatchScorer.DurationWithinBandPoints +
            CrossPlatformMatchScorer.DelayAlignedReleasePoints);
        score.Should().NotBe(
            CrossPlatformMatchScorer.DurationWithinBandPoints +
            CrossPlatformMatchScorer.DelayAlignedReleasePoints +
            CrossPlatformMatchScorer.SameCalendarDayReleasePoints);
    }

    [Fact(DisplayName =
        "YouTube publish within three days of audioRelease + YouTubePublishingDelay earns near-delay " +
        "proximity points (25) so delay difference is scored without requiring full ±1-day alignment.")]
    public void Near_delay_aligned_release_earns_partial_proximity_points()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var delay = podcast.YouTubePublishingDelay();
        var youTubeRelease = DomainTestFixture.UtcAtTime(-40, TimeSpan.FromHours(15));
        var expectedAudio = youTubeRelease - delay;
        // Two days off expected audio — outside ±1d full align, inside ±3d near-align; not same calendar day.
        var audioRelease = expectedAudio.Date.AddDays(2).Add(TimeSpan.FromHours(10));
        EpisodeReleaseTolerance.IsYouTubePublishDelayAligned(audioRelease, youTubeRelease, delay)
            .Should().BeFalse();
        EpisodeReleaseTolerance.AreCrossPlatformReleasesOnSameCalendarDay(audioRelease, youTubeRelease)
            .Should().BeFalse();
        var length = TimeSpan.FromMinutes(55);
        var (youTube, audio) = CreateYouTubeAudioPair(
            podcast,
            youTubeRelease,
            audioRelease,
            length,
            "Alpha market briefing on early catalogue drift signals",
            "Omega wellness interview about unrelated guest journeys");

        // Act
        var score = CrossPlatformMatchScorer.Score(youTube, audio, podcast);

        // Assert
        score.Should().Be(
            CrossPlatformMatchScorer.DurationWithinBandPoints +
            CrossPlatformMatchScorer.NearDelayAlignedReleasePoints);
        score.Should().BeLessThan(CrossPlatformMatchScorer.MatchThreshold);
    }

    [Fact(DisplayName =
        "Fifty-nine is below threshold and sixty meets it — MeetsMatchThreshold uses inclusive comparison.")]
    public void Match_threshold_is_inclusive_at_sixty()
    {
        // Arrange
        // Assert
        CrossPlatformMatchScorer.MatchThreshold.Should().Be(60);
        (CrossPlatformMatchScorer.DurationWithinBandPoints +
         CrossPlatformMatchScorer.SameCalendarDayReleasePoints)
            .Should().Be(60);
        (CrossPlatformMatchScorer.DurationWithinBandPoints +
         CrossPlatformMatchScorer.WeakCatalogueReleasePoints)
            .Should().Be(45);
        (45 + CrossPlatformMatchScorer.SubstringTitlePoints).Should().Be(65);
        (45 + CrossPlatformMatchScorer.FuzzyTitlePoints).Should().Be(70);
        // Document the 59-vs-60 boundary relative to composed constants.
        (CrossPlatformMatchScorer.MatchThreshold - 1).Should().Be(59);
    }

    [Fact(DisplayName =
        "Score is symmetric for YouTube-stored/audio-incoming and audio-stored/YouTube-incoming argument order.")]
    public void Score_is_symmetric_for_youtube_and_audio_argument_order()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var (youTube, audio) = CreateDelayAlignedDivergentPair(podcast);

        // Act
        // Assert
        CrossPlatformMatchScorer.Score(youTube, audio, podcast)
            .Should().Be(CrossPlatformMatchScorer.Score(audio, youTube, podcast));
    }

    [Fact(DisplayName =
        "Weak catalogue-day release plus duration plus one shared classified subject scores 60 and " +
        "meets the match threshold without title confidence.")]
    public void Weak_catalogue_release_with_one_shared_subject_meets_threshold()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var sharedSubject = _fixture.CreateTitle(3);
        var (youTube, audio) = _fixture.CreateNegativeDelayNonMatchingPair(podcast);
        youTube.Subjects = [sharedSubject];
        audio.Subjects = [sharedSubject];

        // Act
        var score = CrossPlatformMatchScorer.Score(youTube, audio, podcast);

        // Assert
        score.Should().Be(
            CrossPlatformMatchScorer.DurationWithinBandPoints +
            CrossPlatformMatchScorer.WeakCatalogueReleasePoints +
            CrossPlatformMatchScorer.SingleSharedSubjectPoints);
        CrossPlatformMatchScorer.MeetsMatchThreshold(youTube, audio, podcast).Should().BeTrue();
    }

    [Fact(DisplayName =
        "Podcast default subject alone does not contribute subject points on cross-platform scoring.")]
    public void Default_subject_overlap_does_not_contribute_points()
    {
        // Arrange
        var defaultSubject = _fixture.CreateTitle(2);
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        podcast.DefaultSubject = defaultSubject;
        var (youTube, audio) = _fixture.CreateNegativeDelayNonMatchingPair(podcast);
        youTube.Subjects = [defaultSubject];
        audio.Subjects = [defaultSubject];

        // Act
        var score = CrossPlatformMatchScorer.Score(youTube, audio, podcast);

        // Assert
        score.Should().Be(
            CrossPlatformMatchScorer.DurationWithinBandPoints +
            CrossPlatformMatchScorer.WeakCatalogueReleasePoints);
        CrossPlatformMatchScorer.MeetsMatchThreshold(youTube, audio, podcast).Should().BeFalse();
    }

    [Fact(DisplayName =
        "Ignored subjects and underscore-prefixed subjects do not contribute cross-platform subject points.")]
    public void Ignored_and_underscore_subjects_do_not_contribute_points()
    {
        // Arrange
        var ignoredSubject = _fixture.CreateTitle(2);
        var underscoreSubject = "_" + _fixture.CreateTitle(2);
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        podcast.IgnoredSubjects = [ignoredSubject];
        var (youTube, audio) = _fixture.CreateNegativeDelayNonMatchingPair(podcast);
        youTube.Subjects = [ignoredSubject, underscoreSubject];
        audio.Subjects = [ignoredSubject, underscoreSubject];

        // Act
        var score = CrossPlatformMatchScorer.Score(youTube, audio, podcast);

        // Assert
        score.Should().Be(
            CrossPlatformMatchScorer.DurationWithinBandPoints +
            CrossPlatformMatchScorer.WeakCatalogueReleasePoints);
        CrossPlatformMatchScorer.MeetsMatchThreshold(youTube, audio, podcast).Should().BeFalse();
    }

    private (Episode YouTube, Episode Audio) CreateDelayAlignedDivergentPair(Podcast podcast)
    {
        var delay = podcast.YouTubePublishingDelay();
        var youTubeRelease = DomainTestFixture.UtcAtTime(-40, TimeSpan.FromHours(14));
        var audioRelease = youTubeRelease - delay;
        var length = TimeSpan.FromMinutes(62) + TimeSpan.FromSeconds(38);
        return CreateYouTubeAudioPair(
            podcast,
            youTubeRelease,
            audioRelease,
            length,
            "The Neighborhood Scheme: Shocking Truth About Wellness Influencer Networks",
            "She Spent a Fortune in a Wellness Scheme with a Guest: New parenthood and a decade lost");
    }

    private (Episode YouTube, Episode Audio) CreateYouTubeAudioPair(
        Podcast podcast,
        DateTime youTubeRelease,
        DateTime audioRelease,
        TimeSpan length,
        string youTubeTitle,
        string audioTitle)
    {
        var youTube = _fixture.CreateStoredEpisodeWithYouTubeOnly(
            podcast, youTubeRelease, length, youTubeTitle);
        var audio = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithTitle(audioTitle)
            .WithRelease(audioRelease)
            .WithDuration(length));
        return (youTube, audio);
    }
}
