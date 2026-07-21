using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Text.Matchers;

namespace RedditPodcastPoster.Episodes.Matching;

/// <summary>
/// Composite confidence score for YouTube↔audio cross-platform merges.
/// Signals add; match when total ≥ <see cref="MatchThreshold"/>.
/// Delay-aligned release + duration reaches threshold without title confidence.
/// Weak catalogue-day (or early-within-negative-delay) release + duration alone does not
/// (#869 protection). Matching episode descriptions supply the same confidence as a fuzzy title.
/// </summary>
public static class CrossPlatformMatchScorer
{
    public const int MatchThreshold = 60;

    public const int DelayAlignedReleasePoints = 40;
    public const int SameCalendarDayReleasePoints = 30;
    public const int WeakCatalogueReleasePoints = 15;
    public const int DurationWithinBandPoints = 30;
    public const int FuzzyTitlePoints = 25;
    public const int SubstringTitlePoints = 20;
    public const int FuzzyDescriptionPoints = FuzzyTitlePoints;

    private const int MinFuzzyTitleScore = 70;
    private const int MinFuzzyDescriptionScore = 70;
    private const int DescriptionCompareMaxChars = 500;

    /// <summary>
    /// Scores a YouTube↔audio pair that already passed a release-strategy match and
    /// duration-within-band checks. Title is supporting evidence, not a hard veto.
    /// </summary>
    public static int Score(
        Episode existingEpisode,
        Episode incomingEpisode,
        Podcast podcast)
    {
        var score = DurationWithinBandPoints;
        score += ScoreReleaseStrength(existingEpisode, incomingEpisode, podcast);
        score += ScoreTitle(existingEpisode, incomingEpisode);
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

        var delay = podcast.YouTubePublishingDelay();
        if (delay != TimeSpan.Zero &&
            EpisodeReleaseTolerance.IsYouTubePublishDelayAligned(
                audioSide.Release,
                youTubeSide.Release,
                delay))
        {
            return DelayAlignedReleasePoints;
        }

        if (EpisodeReleaseTolerance.AreCrossPlatformReleasesOnSameCalendarDay(
                audioSide.Release,
                youTubeSide.Release))
        {
            return SameCalendarDayReleasePoints;
        }

        return WeakCatalogueReleasePoints;
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
