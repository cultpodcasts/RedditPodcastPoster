using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Text.Matchers;

namespace RedditPodcastPoster.Episodes.Matching;

/// <summary>
/// Composite confidence score for YouTube↔audio cross-platform merges.
/// Signals add; match when total ≥ <see cref="MatchThreshold"/>.
/// Release scoring prefers proximity of the YouTube publish to
/// <c>audioRelease + YouTubePublishingDelay</c> (delay is a score signal, not a hard gate).
/// Delay-aligned release + duration reaches threshold without title confidence.
/// Weak catalogue-day release + duration alone does not (#869 protection).
/// Matching episode descriptions or shared subjects supply supporting confidence.
/// </summary>
public static class CrossPlatformMatchScorer
{
    public const int MatchThreshold = 60;

    public const int DelayAlignedReleasePoints = 40;
    public const int NearDelayAlignedReleasePoints = 25;
    public const int SameCalendarDayReleasePoints = 30;
    public const int WeakCatalogueReleasePoints = 15;
    public const int DurationWithinBandPoints = 30;
    public const int FuzzyTitlePoints = 25;
    public const int SubstringTitlePoints = 20;
    public const int FuzzyDescriptionPoints = FuzzyTitlePoints;
    public const int SingleSharedSubjectPoints = CatalogueMatchScorer.SingleSharedSubjectPoints;
    public const int MultipleSharedSubjectPoints = CatalogueMatchScorer.MultipleSharedSubjectPoints;

    /// <summary>Full delay-alignment credit when YouTube is within this of expected publish.</summary>
    public static readonly TimeSpan DelayAlignedWindow =
        EpisodeReleaseTolerance.YouTubePublishDelayMatchThreshold;

    /// <summary>Partial delay-proximity credit outside the full window but still near expected.</summary>
    public static readonly TimeSpan NearDelayAlignedWindow = TimeSpan.FromDays(3);

    private const int MinFuzzyTitleScore = 70;
    private const int MinFuzzyDescriptionScore = 70;
    private const int DescriptionCompareMaxChars = 500;

    /// <summary>
    /// Scores a YouTube↔audio pair that already passed a release-strategy match and
    /// duration-within-band checks. Title, description, shared subjects, and how close
    /// YouTube’s publish is to <c>audio + YouTubePublishingDelay</c> are supporting evidence.
    /// </summary>
    public static int Score(
        Episode existingEpisode,
        Episode incomingEpisode,
        Podcast podcast)
    {
        var score = DurationWithinBandPoints;
        score += ScoreReleaseStrength(existingEpisode, incomingEpisode, podcast);
        score += ScoreTitle(existingEpisode, incomingEpisode);
        score += ScoreSubjects(existingEpisode, incomingEpisode, podcast);
        return score;
    }

    public static bool MeetsMatchThreshold(
        Episode existingEpisode,
        Episode incomingEpisode,
        Podcast podcast) =>
        Score(existingEpisode, incomingEpisode, podcast) >= MatchThreshold;

    private static int ScoreReleaseStrength(
        Episode existingEpisode,
        Episode incomingEpisode,
        Podcast podcast)
    {
        if (!TryGetYouTubeAndAudioSides(existingEpisode, incomingEpisode, out var youTubeSide, out var audioSide))
        {
            return WeakCatalogueReleasePoints;
        }

        var delayProximityPoints = ScoreDelayProximity(
            audioSide.Release,
            youTubeSide.Release,
            podcast.YouTubePublishingDelay());
        var calendarDayPoints = EpisodeReleaseTolerance.AreCrossPlatformReleasesOnSameCalendarDay(
                audioSide.Release,
                youTubeSide.Release)
            ? SameCalendarDayReleasePoints
            : 0;

        var best = Math.Max(delayProximityPoints, calendarDayPoints);
        return best > 0 ? best : WeakCatalogueReleasePoints;
    }

    /// <summary>
    /// Points from |youTubeRelease − (audioRelease + delay)|. Zero delay configured → 0
    /// (calendar-day / weak tiers apply instead).
    /// </summary>
    private static int ScoreDelayProximity(
        DateTime audioRelease,
        DateTime youTubeRelease,
        TimeSpan publishingDelay)
    {
        if (publishingDelay == TimeSpan.Zero)
        {
            return 0;
        }

        var expectedPublish = audioRelease.Add(publishingDelay);
        var delta = TimeSpan.FromTicks(Math.Abs((youTubeRelease - expectedPublish).Ticks));
        if (delta < DelayAlignedWindow)
        {
            return DelayAlignedReleasePoints;
        }

        if (delta < NearDelayAlignedWindow)
        {
            return NearDelayAlignedReleasePoints;
        }

        return 0;
    }

    private static int ScoreTitle(Episode existingEpisode, Episode incomingEpisode)
    {
        if (FuzzyMatcher.IsMatch(
                existingEpisode.Title,
                incomingEpisode,
                e => e.Title,
                MinFuzzyTitleScore))
        {
            return FuzzyTitlePoints;
        }

        if (TitlesShareSubstringRelationship(existingEpisode.Title, incomingEpisode.Title))
        {
            return SubstringTitlePoints;
        }

        if (DescriptionsFuzzyMatch(existingEpisode.Description, incomingEpisode.Description))
        {
            // Same weight as fuzzy title: marketing titles often diverge while show notes match
            // (YouTube teasers later renamed to match Spotify/Apple catalogue titles).
            return FuzzyDescriptionPoints;
        }

        return 0;
    }

    private static int ScoreSubjects(
        Episode existingEpisode,
        Episode incomingEpisode,
        Podcast podcast)
    {
        var filters = new CatalogueSubjectScoreFilters(
            podcast.DefaultSubject,
            podcast.IgnoredSubjects);
        var shared = CatalogueMatchScorer.CountSharedSubjects(
            existingEpisode.Subjects,
            incomingEpisode.Subjects,
            filters);
        return shared switch
        {
            >= 2 => MultipleSharedSubjectPoints,
            1 => SingleSharedSubjectPoints,
            _ => 0
        };
    }

    private static bool DescriptionsFuzzyMatch(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        var leftSample = TruncateForFuzzyCompare(left);
        var rightSample = TruncateForFuzzyCompare(right);
        return FuzzyMatcher.IsMatch(leftSample, rightSample, s => s, MinFuzzyDescriptionScore);
    }

    private static string TruncateForFuzzyCompare(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= DescriptionCompareMaxChars
            ? trimmed
            : trimmed[..DescriptionCompareMaxChars];
    }

    private static bool TryGetYouTubeAndAudioSides(
        Episode existingEpisode,
        Episode incomingEpisode,
        out Episode youTubeSide,
        out Episode audioSide)
    {
        if (existingEpisode.HasYouTubeIdentity() && !incomingEpisode.HasYouTubeIdentity())
        {
            youTubeSide = existingEpisode;
            audioSide = incomingEpisode;
            return true;
        }

        if (incomingEpisode.HasYouTubeIdentity() && !existingEpisode.HasYouTubeIdentity())
        {
            youTubeSide = incomingEpisode;
            audioSide = existingEpisode;
            return true;
        }

        youTubeSide = existingEpisode;
        audioSide = incomingEpisode;
        return false;
    }

    private static bool TitlesShareSubstringRelationship(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return left.Contains(right, StringComparison.OrdinalIgnoreCase) ||
               right.Contains(left, StringComparison.OrdinalIgnoreCase);
    }
}
