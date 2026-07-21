using System.Net;
using System.Text.RegularExpressions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Text;

namespace RedditPodcastPoster.Episodes.Matching;

public sealed partial class EpisodePlatformMatcher
{
    private const int CatalogueMinFuzzyScore = 65;
    private const int CatalogueSameLengthMinFuzzyScore = 35;
    private const int CatalogueMinSameLengthFuzzyScore = 80;
    private static readonly long CatalogueTimeDifferenceThreshold = TimeSpan.FromSeconds(30).Ticks;
    private static readonly long CatalogueBroaderTimeDifferenceThreshold = TimeSpan.FromSeconds(90).Ticks;
    private static readonly long CatalogueYouTubeDiscoveredDurationThreshold = TimeSpan.FromMinutes(5).Ticks;
    private static readonly TimeSpan CatalogueSameReleaseThreshold = TimeSpan.FromHours(3);
    private static readonly TimeSpan CatalogueYouTubeDiscoveredReleaseThreshold = TimeSpan.FromHours(12);

    /// <summary>
    /// Selects the best catalogue episode match by title and duration heuristics
    /// (consolidated from Spotify <c>SpotifySearchResultFinder</c> and Apple episode resolver).
    /// </summary>
    public Episode? FindCatalogueMatchByLength(
        Episode probe,
        IEnumerable<Episode> candidates,
        Podcast podcast,
        Regex? episodeMatchRegex,
        CatalogueMatchByLengthOptions options,
        Func<Episode, bool>? reducer = null)
    {
        var requestEpisodeTitle = WebUtility.HtmlDecode(probe.Title.Trim());

        var matches = candidates.Where(x =>
        {
            var trimmedEpisodeTitle = WebUtility.HtmlDecode(x.Title.Trim());
            return trimmedEpisodeTitle == requestEpisodeTitle ||
                   trimmedEpisodeTitle.Contains(requestEpisodeTitle) ||
                   requestEpisodeTitle.Contains(trimmedEpisodeTitle);
        });
        if (reducer != null)
        {
            matches = matches.Where(reducer);
        }

        var match = matches.MaxBy(x => x.Title);
        if (match != null)
        {
            return match;
        }

        IList<Episode> sampleList = reducer != null
            ? candidates.Where(reducer).ToList()
            : candidates.ToList();

        if (options.EnrichingYouTubeDiscoveredEpisode &&
            probe.Length > TimeSpan.Zero)
        {
            var youTubeDiscoveredMatch = FindYouTubeDiscoveredCatalogueMatch(probe, sampleList);
            if (youTubeDiscoveredMatch != null)
            {
                return youTubeDiscoveredMatch;
            }
        }

        if (probe.Length <= TimeSpan.Zero)
        {
            return null;
        }

        var durationThreshold = GetCatalogueDurationThresholdTicks(options);
        var sameLength = sampleList
            .Where(x => Math.Abs((x.Length - probe.Length).Ticks) < durationThreshold)
            .ToList();

        // YouTube-discovered enrichment already attempted title-confident matching above.
        // Do not fall through to unique-duration acceptance — that snipes wrong-week audio
        // when catalogue titles diverge.
        if (sameLength.Count == 1 &&
            options.AcceptUniqueDurationWithoutTitleMatch &&
            !options.EnrichingYouTubeDiscoveredEpisode)
        {
            return sameLength[0];
        }

        if (sameLength.Count > 1)
        {
            return FuzzyMatcher.Match(probe.Title, sameLength, x => x.Title, CatalogueMinSameLengthFuzzyScore);
        }

        match = sameLength.SingleOrDefault(x =>
            FuzzyMatcher.IsMatch(probe.Title, x, y => y.Title, CatalogueMinFuzzyScore));

        if (match != null)
        {
            return match;
        }

        if (probe.Release != DateTime.MinValue)
        {
            // Same-release fallback must still respect duration when both sides have one: on daily
            // shows a different same-day episode otherwise wins on a weak fuzzy-title score alone.
            sameLength = sampleList.Where(x =>
                Math.Abs((x.Release - probe.Release).Ticks) < CatalogueSameReleaseThreshold.Ticks &&
                (x.Length <= TimeSpan.Zero ||
                 Math.Abs((x.Length - probe.Length).Ticks) < CatalogueBroaderTimeDifferenceThreshold)).ToList();
        }

        if (options.ReleaseAuthority == Service.YouTube)
        {
            sameLength = sampleList.Where(x =>
                Math.Abs((x.Length - probe.Length).Ticks) < CatalogueBroaderTimeDifferenceThreshold).ToList();
        }

        return FuzzyMatcher.Match(probe.Title, sameLength, x => x.Title, CatalogueSameLengthMinFuzzyScore);
    }

    /// <summary>
    /// Selects the best catalogue episode match by title and release date heuristics.
    /// </summary>
    public Episode? FindCatalogueMatchByDate(
        Episode probe,
        IEnumerable<Episode> candidates,
        Podcast podcast,
        Regex? episodeMatchRegex,
        Func<Episode, bool>? reducer = null)
    {
        var lowerTitle = probe.Title.ToLowerInvariant();
        var matches = candidates.Where(x =>
        {
            var itemLowerTitle = x.Title.Trim().ToLowerInvariant();
            return itemLowerTitle == lowerTitle || itemLowerTitle.Contains(lowerTitle) ||
                   lowerTitle.Contains(itemLowerTitle);
        });
        if (reducer != null)
        {
            matches = matches.Where(reducer);
        }

        var match = matches.FirstOrDefault();
        if (match != null)
        {
            return match;
        }

        if (probe.Release == DateTime.MinValue)
        {
            return null;
        }

        var sameDateMatches = candidates.Where(x =>
        {
            if (reducer != null && !reducer(x))
            {
                return false;
            }

            var episodeReleaseDateTime = x.Release;
            if (episodeReleaseDateTime == DateTime.MinValue)
            {
                return true;
            }

            var episodeReleaseDate = DateOnly.FromDateTime(episodeReleaseDateTime);
            var expectedDateOnly = DateOnly.FromDateTime(probe.Release);
            var dateDiff = Math.Abs(expectedDateOnly.DayNumber - episodeReleaseDate.DayNumber);

            return episodeReleaseDate == expectedDateOnly || dateDiff <= 1;
        }).ToList();

        if (sameDateMatches.Count > 1)
        {
            return FuzzyMatcher.Match(probe.Title, sameDateMatches, x => x.Title, CatalogueMinFuzzyScore);
        }

        return sameDateMatches.SingleOrDefault(x =>
            FuzzyMatcher.IsMatch(probe.Title, x, y => y.Title, CatalogueMinFuzzyScore));
    }

    /// <summary>
    /// Returns true when a catalogue item's release aligns with the probe for platform lookup filtering
    /// (replaces direct EpisodeReleaseMatchTolerance.SpotifyCatalogueReleaseMatches at enricher boundaries).
    /// </summary>
    public bool CatalogueReleaseMatches(Episode probe, Episode catalogueItem, Podcast podcast)
    {
        if (probe.Release == DateTime.MinValue)
        {
            return false;
        }

        var referenceLength = probe.Length > TimeSpan.Zero ? probe.Length : catalogueItem.Length;
        var toleranceTicks = EpisodeReleaseTolerance.GetToleranceTicks(podcast, referenceLength);
        return EpisodeReleaseTolerance.AudioCatalogueReleaseMatches(
            catalogueItem.Release,
            probe.Release,
            toleranceTicks,
            podcast);
    }

    /// <summary>
    /// Returns true when probe and catalogue item match via domain heuristics (identity, title, duration, release).
    /// </summary>
    public bool IsCatalogueMatch(
        Episode probe,
        Episode catalogueItem,
        Podcast podcast,
        Regex? episodeMatchRegex) =>
        IsMatch(probe, catalogueItem, episodeMatchRegex, podcast, []);

    private static Episode? FindYouTubeDiscoveredCatalogueMatch(
        Episode probe,
        IList<Episode> sampleList)
    {
        var titleConfidentCandidates = sampleList
            .Where(x => HasCatalogueTitleConfidence(probe.Title, x.Title))
            .ToList();

        if (titleConfidentCandidates.Count == 0)
        {
            return null;
        }

        return FindYouTubeDiscoveredCatalogueMatchByDuration(
            titleConfidentCandidates,
            probe.Length,
            probe.Release);
    }

    private static bool HasCatalogueTitleConfidence(string probeTitle, string candidateTitle)
    {
        var left = WebUtility.HtmlDecode(probeTitle.Trim());
        var right = WebUtility.HtmlDecode(candidateTitle.Trim());

        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        if (left.Equals(right, StringComparison.OrdinalIgnoreCase) ||
            left.Contains(right, StringComparison.OrdinalIgnoreCase) ||
            right.Contains(left, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return FuzzyMatcher.IsMatch(left, new Episode { Title = right }, e => e.Title, CatalogueMinFuzzyScore);
    }

    private static Episode? FindYouTubeDiscoveredCatalogueMatchByDuration(
        IList<Episode> sampleList,
        TimeSpan episodeLength,
        DateTime released)
    {
        var sameLength = sampleList
            .Where(x => Math.Abs((x.Length - episodeLength).Ticks) < CatalogueYouTubeDiscoveredDurationThreshold)
            .ToList();

        if (sameLength.Count == 1)
        {
            return sameLength[0];
        }

        if (sameLength.Count > 1 && released != DateTime.MinValue)
        {
            return sameLength.MinBy(x => Math.Abs((x.Release - released).Ticks));
        }

        if (released != DateTime.MinValue)
        {
            var releaseMatches = sampleList
                .Where(x => Math.Abs((x.Release - released).Ticks) <
                            CatalogueYouTubeDiscoveredReleaseThreshold.Ticks)
                .ToList();
            if (releaseMatches.Count == 1)
            {
                return releaseMatches[0];
            }

            if (releaseMatches.Count > 1)
            {
                return releaseMatches.MinBy(x => Math.Abs((x.Release - released).Ticks));
            }
        }

        return null;
    }

    private static long GetCatalogueDurationThresholdTicks(CatalogueMatchByLengthOptions options) =>
        options.EnrichingYouTubeDiscoveredEpisode || options.AcceptUniqueDurationWithoutTitleMatch
            ? CatalogueYouTubeDiscoveredDurationThreshold
            : CatalogueTimeDifferenceThreshold;
}
