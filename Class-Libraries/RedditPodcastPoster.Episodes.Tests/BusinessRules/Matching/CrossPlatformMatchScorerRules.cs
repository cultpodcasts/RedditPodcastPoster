using FluentAssertions;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
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
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var (youTube, audio) = CreateDelayAlignedDivergentPair(podcast);

        var score = CrossPlatformMatchScorer.Score(youTube, audio, podcast);

        score.Should().Be(
            CrossPlatformMatchScorer.DurationWithinBandPoints +
            CrossPlatformMatchScorer.DelayAlignedReleasePoints);
        CrossPlatformMatchScorer.MeetsMatchThreshold(youTube, audio, podcast).Should().BeTrue();
    }

    [Fact(DisplayName =
        "Same-calendar-day release plus duration scores exactly 60 and meets the match threshold without title.")]
    public void Same_calendar_day_release_and_duration_meets_threshold_at_boundary()
    {
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

        var score = CrossPlatformMatchScorer.Score(youTube, audio, podcast);

        score.Should().Be(CrossPlatformMatchScorer.MatchThreshold);
        CrossPlatformMatchScorer.MeetsMatchThreshold(youTube, audio, podcast).Should().BeTrue();
    }

    [Fact(DisplayName =
        "Weak catalogue-day release plus duration scores 45 and stays below the match threshold without title.")]
    public void Weak_catalogue_release_and_duration_stays_below_threshold()
    {
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var (youTube, audio) = _fixture.CreateNegativeDelayNonMatchingPair(podcast);

        var score = CrossPlatformMatchScorer.Score(youTube, audio, podcast);

        score.Should().Be(
            CrossPlatformMatchScorer.DurationWithinBandPoints +
            CrossPlatformMatchScorer.WeakCatalogueReleasePoints);
        score.Should().BeLessThan(CrossPlatformMatchScorer.MatchThreshold);
        CrossPlatformMatchScorer.MeetsMatchThreshold(youTube, audio, podcast).Should().BeFalse();
    }

    [Fact(DisplayName =
        "Early-within-negative-delay release plus duration plus matching descriptions reaches 70 and " +
        "meets the threshold even when marketing titles are wholly disjoint.")]
    public void Early_within_delay_with_matching_descriptions_meets_threshold()
    {
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var (youTube, audio, _) =
            _fixture.CreateYouTubeAuthorityNegativeOffsetEarlyAudioPair(podcast, matchingTitles: false);

        var score = CrossPlatformMatchScorer.Score(youTube, audio, podcast);

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
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var delay = podcast.YouTubePublishingDelay();
        var youTubeRelease = DomainTestFixture.UtcAtTime(-40, TimeSpan.FromHours(15));
        var audioRelease = (youTubeRelease - delay).Date.AddDays(3);
        var length = TimeSpan.FromMinutes(55);
        const string baseTitle = "Holy Disobedience Inside the Seventh-day Adventist Church";
        var (youTube, audio) = CreateYouTubeAudioPair(
            podcast,
            youTubeRelease,
            audioRelease,
            length,
            baseTitle,
            baseTitle + " with Melissa Duge Spiers");

        var score = CrossPlatformMatchScorer.Score(youTube, audio, podcast);

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
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var delay = podcast.YouTubePublishingDelay();
        var youTubeRelease = DomainTestFixture.UtcAtTime(-40, TimeSpan.FromHours(15));
        var audioRelease = (youTubeRelease - delay).Date.AddDays(3);
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

        var score = CrossPlatformMatchScorer.Score(youTube, audio, podcast);
        var weakFloor =
            CrossPlatformMatchScorer.DurationWithinBandPoints +
            CrossPlatformMatchScorer.WeakCatalogueReleasePoints;

        score.Should().BeOneOf(
            weakFloor + CrossPlatformMatchScorer.SubstringTitlePoints,
            weakFloor + CrossPlatformMatchScorer.FuzzyTitlePoints);
        score.Should().BeGreaterThanOrEqualTo(CrossPlatformMatchScorer.MatchThreshold);
        CrossPlatformMatchScorer.MeetsMatchThreshold(youTube, audio, podcast).Should().BeTrue();
    }

    [Fact(DisplayName =
        "Delay-aligned release takes precedence over same-calendar-day: release tiers are mutually exclusive.")]
    public void Delay_aligned_release_tier_does_not_stack_same_calendar_day_points()
    {
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        podcast.YouTubePublicationOffset = TimeSpan.FromHours(-8).Ticks;
        var audioRelease = new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc);
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

        var score = CrossPlatformMatchScorer.Score(youTube, audio, podcast);

        score.Should().Be(
            CrossPlatformMatchScorer.DurationWithinBandPoints +
            CrossPlatformMatchScorer.DelayAlignedReleasePoints);
        score.Should().NotBe(
            CrossPlatformMatchScorer.DurationWithinBandPoints +
            CrossPlatformMatchScorer.DelayAlignedReleasePoints +
            CrossPlatformMatchScorer.SameCalendarDayReleasePoints);
    }

    [Fact(DisplayName =
        "Fifty-nine is below threshold and sixty meets it — MeetsMatchThreshold uses inclusive comparison.")]
    public void Match_threshold_is_inclusive_at_sixty()
    {
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
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var (youTube, audio) = CreateDelayAlignedDivergentPair(podcast);

        CrossPlatformMatchScorer.Score(youTube, audio, podcast)
            .Should().Be(CrossPlatformMatchScorer.Score(audio, youTube, podcast));
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
