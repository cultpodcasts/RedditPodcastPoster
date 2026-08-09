using System.Net;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Text.Matchers;

namespace RedditPodcastPoster.Episodes.Matching;

/// <summary>
/// Multi-criteria score for YouTube-discovered Spotify/Apple catalogue enrichment.
/// Signals add; match when total ≥ <see cref="MatchThreshold"/>.
/// Duration + in-window release alone stays below threshold (Aug 2026 wrong-attach protection).
/// Title, description, or subject similarity supply the remaining confidence.
/// When either side lacks duration (e.g. Apple omitting <c>durationInMilliseconds</c>),
/// duration points are skipped and duration band is not a hard fail — release window plus
/// title/description/subjects must still clear the threshold (release alone cannot).
/// When both sides have duration, the allowed gap is
/// <c>max(<see cref="DurationBandFloor"/>, <see cref="DurationBandProportionOfShorter"/> × shorter)</c>
/// so long Apple cuts with a few minutes of ads still match while short episodes keep a
/// five-minute floor (Aug 2026: long YouTube cut vs slightly longer Apple audio).
/// Spotify and Apple catalogue items are both windowed on calendar days, not elapsed hours,
/// because audio slots and YouTube publishes on the same day can be far more than twelve hours apart.
/// </summary>
public static class CatalogueMatchScorer
{
    public const int MatchThreshold = 60;

    public const int DurationWithinBandPoints = 30;
    public const int SameCalendarDayReleasePoints = 25;
    public const int WeakInWindowReleasePoints = 15;
    public const int FuzzyTitlePoints = 25;
    public const int SubstringTitlePoints = 20;
    public const int FuzzyDescriptionPoints = FuzzyTitlePoints;
    public const int SingleSharedSubjectPoints = 15;
    public const int MultipleSharedSubjectPoints = 25;

    /// <summary>Minimum absolute duration gap allowed when both sides report length.</summary>
    public static readonly TimeSpan DurationBandFloor = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Fraction of the shorter duration added on top of <see cref="DurationBandFloor"/> for long episodes.
    /// </summary>
    public const double DurationBandProportionOfShorter = 0.10;

    private const int MinFuzzyTitleScore = 65;
    private const int MinFuzzyDescriptionScore = 70;
    private const int DescriptionCompareMaxChars = 500;

    /// <summary>
    /// Allowed absolute duration delta when both sides have length:
    /// <c>max(DurationBandFloor, DurationBandProportionOfShorter × shorter)</c>.
    /// </summary>
    public static TimeSpan GetDurationBand(TimeSpan left, TimeSpan right)
    {
        var shorter = left <= right ? left : right;
        if (shorter <= TimeSpan.Zero)
        {
            return DurationBandFloor;
        }

        var proportional = TimeSpan.FromTicks((long)(shorter.Ticks * DurationBandProportionOfShorter));
        return proportional > DurationBandFloor ? proportional : DurationBandFloor;
    }

    /// <summary>
    /// Scores a probe against a catalogue candidate within the YouTube-discovered release window.
    /// When both sides have duration, requires the proportional duration band; otherwise scores without
    /// duration points so Apple catalogue gaps can still match on title/subjects.
    /// </summary>
    public static int Score(
        Episode probe,
        Episode catalogueItem,
        CatalogueSubjectScoreFilters? subjectFilters = null)
    {
        var probeHasDuration = probe.Length > TimeSpan.Zero;
        var catalogueHasDuration = catalogueItem.Length > TimeSpan.Zero;
        var durationInBand = false;

        if (probeHasDuration && catalogueHasDuration)
        {
            var band = GetDurationBand(probe.Length, catalogueItem.Length);
            if (Math.Abs((catalogueItem.Length - probe.Length).Ticks) >= band.Ticks)
            {
                return 0;
            }

            durationInBand = true;
        }

        if (probe.Release == DateTime.MinValue ||
            !IsWithinReleaseWindow(probe.Release, catalogueItem))
        {
            return 0;
        }

        var score = durationInBand ? DurationWithinBandPoints : 0;
        score += ScoreRelease(probe.Release, catalogueItem);
        score += ScoreTitle(probe.Title, catalogueItem.Title);
        score += ScoreDescription(probe.Description, catalogueItem.Description);
        score += ScoreSubjects(probe.Subjects, catalogueItem.Subjects, subjectFilters);
        return score;
    }

    public static bool MeetsMatchThreshold(
        Episode probe,
        Episode catalogueItem,
        CatalogueSubjectScoreFilters? subjectFilters = null) =>
        Score(probe, catalogueItem, subjectFilters) >= MatchThreshold;

    public static int CountSharedSubjects(
        IEnumerable<string>? left,
        IEnumerable<string>? right,
        CatalogueSubjectScoreFilters? filters = null)
    {
        var leftSet = FilterSubjectNames(left, filters);
        if (leftSet.Count == 0)
        {
            return 0;
        }

        var rightSet = FilterSubjectNames(right, filters);
        if (rightSet.Count == 0)
        {
            return 0;
        }

        return leftSet.Count(rightSet.Contains);
    }

    public static Episode? SelectBestMatch(
        Episode probe,
        IEnumerable<Episode> candidates,
        CatalogueSubjectScoreFilters? subjectFilters = null)
    {
        Episode? best = null;
        var bestScore = 0;
        var bestSharedSubjects = -1;
        var bestReleaseDelta = long.MaxValue;

        foreach (var candidate in candidates)
        {
            var score = Score(probe, candidate, subjectFilters);
            if (score < MatchThreshold)
            {
                continue;
            }

            var sharedSubjects = CountSharedSubjects(probe.Subjects, candidate.Subjects, subjectFilters);
            var releaseDelta = probe.Release == DateTime.MinValue
                ? long.MaxValue
                : Math.Abs((candidate.Release - probe.Release).Ticks);

            if (best == null ||
                score > bestScore ||
                (score == bestScore && sharedSubjects > bestSharedSubjects) ||
                (score == bestScore &&
                 sharedSubjects == bestSharedSubjects &&
                 releaseDelta < bestReleaseDelta))
            {
                best = candidate;
                bestScore = score;
                bestSharedSubjects = sharedSubjects;
                bestReleaseDelta = releaseDelta;
            }
        }

        return best;
    }

    /// <summary>
    /// Spotify and Apple both publish audio on the show's own schedule, which routinely sits more than
    /// twelve hours from the YouTube publish the probe is derived from (Aug 2026 The Indo Daily: audio at
    /// 00:05, YouTube at 18:15 the same day). Both are compared on calendar days rather than elapsed hours.
    /// </summary>
    private static bool IsAudioCatalogueItem(Episode catalogueItem) =>
        !string.IsNullOrWhiteSpace(catalogueItem.SpotifyId) || catalogueItem.AppleId is > 0;

    private static int ScoreRelease(DateTime probeRelease, Episode catalogueItem)
    {
        if (IsAudioCatalogueItem(catalogueItem))
        {
            if (EpisodeReleaseTolerance.AudioCatalogueReleaseMatches(
                    catalogueItem.Release,
                    probeRelease,
                    toleranceTicks: 0,
                    podcast: null))
            {
                var probeDate = DateOnly.FromDateTime(probeRelease);
                var catalogueDate = DateOnly.FromDateTime(catalogueItem.Release);
                return probeDate == catalogueDate
                    ? SameCalendarDayReleasePoints
                    : WeakInWindowReleasePoints;
            }

            return 0;
        }

        var delta = Math.Abs((catalogueItem.Release - probeRelease).Ticks);
        if (delta < TimeSpan.FromHours(12).Ticks)
        {
            return EpisodeReleaseTolerance.AreCrossPlatformReleasesOnSameCalendarDay(
                       catalogueItem.Release,
                       probeRelease)
                ? SameCalendarDayReleasePoints
                : WeakInWindowReleasePoints;
        }

        return 0;
    }

    private static bool IsWithinReleaseWindow(DateTime probeRelease, Episode catalogueItem)
    {
        if (IsAudioCatalogueItem(catalogueItem))
        {
            return EpisodeReleaseTolerance.AudioCatalogueReleaseMatches(
                catalogueItem.Release,
                probeRelease,
                toleranceTicks: 0,
                podcast: null);
        }

        return Math.Abs((catalogueItem.Release - probeRelease).Ticks) <
               TimeSpan.FromHours(12).Ticks;
    }

    private static int ScoreTitle(string probeTitle, string catalogueTitle)
    {
        var left = WebUtility.HtmlDecode(probeTitle.Trim());
        var right = WebUtility.HtmlDecode(catalogueTitle.Trim());
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return 0;
        }

        if (left.Equals(right, StringComparison.OrdinalIgnoreCase) ||
            left.Contains(right, StringComparison.OrdinalIgnoreCase) ||
            right.Contains(left, StringComparison.OrdinalIgnoreCase))
        {
            return SubstringTitlePoints;
        }

        if (FuzzyMatcher.IsMatch(left, new Episode { Title = right }, e => e.Title, MinFuzzyTitleScore))
        {
            return FuzzyTitlePoints;
        }

        return 0;
    }

    private static int ScoreDescription(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return 0;
        }

        var leftSample = TruncateForFuzzyCompare(left);
        var rightSample = TruncateForFuzzyCompare(right);
        return FuzzyMatcher.IsMatch(leftSample, rightSample, s => s, MinFuzzyDescriptionScore)
            ? FuzzyDescriptionPoints
            : 0;
    }

    private static int ScoreSubjects(
        IEnumerable<string>? left,
        IEnumerable<string>? right,
        CatalogueSubjectScoreFilters? filters)
    {
        var shared = CountSharedSubjects(left, right, filters);
        return shared switch
        {
            >= 2 => MultipleSharedSubjectPoints,
            1 => SingleSharedSubjectPoints,
            _ => 0
        };
    }

    private static HashSet<string> FilterSubjectNames(
        IEnumerable<string>? names,
        CatalogueSubjectScoreFilters? filters)
    {
        var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(filters?.DefaultSubject))
        {
            ignored.Add(filters.DefaultSubject);
        }

        if (filters?.IgnoredSubjects != null)
        {
            foreach (var name in filters.IgnoredSubjects)
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    ignored.Add(name);
                }
            }
        }

        return (names ?? [])
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Where(n => !n.StartsWith('_'))
            .Where(n => !ignored.Contains(n))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string TruncateForFuzzyCompare(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= DescriptionCompareMaxChars
            ? trimmed
            : trimmed[..DescriptionCompareMaxChars];
    }
}

/// <summary>
/// Filters applied when scoring subject overlap for catalogue matching.
/// </summary>
public sealed record CatalogueSubjectScoreFilters(
    string? DefaultSubject = null,
    IReadOnlyList<string>? IgnoredSubjects = null);
