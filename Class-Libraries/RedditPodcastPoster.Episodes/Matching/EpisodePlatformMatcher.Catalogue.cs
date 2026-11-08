using System.Text.RegularExpressions;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Text.Matchers;

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
        var matches = candidates.Where(x =>
            EpisodeTitleMatch.EqualsOrContains(x.Title, probe.Title));
        if (reducer != null)
        {
            matches = matches.Where(reducer);
        }

        // Title containment alone must not win across calendar days (daily-show wrong attach).
        // Prefer day-aligned title matches; closest release breaks ties; exact title preferred.
        // YouTube-discovered enrich: require day alignment (no years-apart title fall-back) —
        // unmatched titles go to CatalogueMatchScorer.
        var titleMatch = SelectReleaseAlignedTitleMatch(
            probe,
            matches,
            podcast,
            requireDayAlignment: options.EnrichingYouTubeDiscoveredEpisode);
        if (titleMatch != null)
        {
            return titleMatch;
        }

        IList<Episode> sampleList = reducer != null
            ? candidates.Where(reducer).ToList()
            : candidates.ToList();

        if (options.EnrichingYouTubeDiscoveredEpisode)
        {
            // Multi-criteria score (duration, release, title, description, subjects).
            // Do not fall through to same-release / low fuzzy-score matching — that
            // reintroduces wrong-week Spotify attaches (Aug 2026 incident).
            // Probe/catalogue may lack duration (Apple catalogue gaps); scorer handles that.
            return FindYouTubeDiscoveredCatalogueMatch(probe, sampleList, options);
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

        var fuzzySameLength = sameLength.SingleOrDefault(x =>
            FuzzyMatcher.IsMatch(probe.Title, x, y => y.Title, CatalogueMinFuzzyScore));

        if (fuzzySameLength != null)
        {
            return fuzzySameLength;
        }

        if (probe.ReleaseUtc != DateTime.MinValue)
        {
            // Same-release fallback must still respect duration when both sides have one: on daily
            // shows a different same-day episode otherwise wins on a weak fuzzy-title score alone.
            sameLength = sampleList.Where(x =>
                Math.Abs((x.ReleaseUtc - probe.ReleaseUtc).Ticks) < CatalogueSameReleaseThreshold.Ticks &&
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
        var matches = candidates.Where(x =>
            EpisodeTitleMatch.EqualsOrContains(x.Title, probe.Title));
        if (reducer != null)
        {
            matches = matches.Where(reducer);
        }

        // FirstOrDefault on title containment without a day gate attaches the wrong Spotify
        // row when several episodes share a title substring inside the ±14-day submit band.
        var titleMatch = SelectReleaseAlignedTitleMatch(probe, matches, podcast);
        if (titleMatch != null)
        {
            return titleMatch;
        }

        if (probe.ReleaseUtc == DateTime.MinValue)
        {
            return null;
        }

        var sameDateMatches = candidates.Where(x =>
        {
            if (reducer != null && !reducer(x))
            {
                return false;
            }

            var episodeReleaseDateTime = x.ReleaseUtc;
            if (episodeReleaseDateTime == DateTime.MinValue)
            {
                return true;
            }

            var episodeReleaseDate = DateOnly.FromDateTime(episodeReleaseDateTime);
            var expectedDateOnly = DateOnly.FromDateTime(probe.ReleaseUtc);
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
        if (probe.ReleaseUtc == DateTime.MinValue)
        {
            return false;
        }

        var referenceLength = probe.Length > TimeSpan.Zero ? probe.Length : catalogueItem.Length;
        var toleranceTicks = EpisodeReleaseTolerance.GetToleranceTicks(podcast, referenceLength);
        return EpisodeReleaseTolerance.AudioCatalogueReleaseMatches(
            catalogueItem.ReleaseUtc,
            probe.ReleaseUtc,
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
        IList<Episode> sampleList,
        CatalogueMatchByLengthOptions options)
    {
        var subjectFilters = new CatalogueSubjectScoreFilters(
            options.DefaultSubject,
            options.IgnoredSubjects);
        return CatalogueMatchScorer.SelectBestMatch(probe, sampleList, subjectFilters, options.YouTubePublished);
    }

    private static long GetCatalogueDurationThresholdTicks(CatalogueMatchByLengthOptions options) =>
        options.EnrichingYouTubeDiscoveredEpisode || options.AcceptUniqueDurationWithoutTitleMatch
            ? CatalogueYouTubeDiscoveredDurationThreshold
            : CatalogueTimeDifferenceThreshold;

    /// <summary>
    /// Among equals-or-contains title candidates, keep only those within Spotify catalogue day
    /// tolerance of the probe. Prefer exact normalised title, then closest release. When
    /// <paramref name="requireDayAlignment"/> is true, unknown (<see cref="DateTime.MinValue"/>)
    /// catalogue releases are not treated as day-aligned and a null return falls through to
    /// scoring. When false, unknown-release candidates remain eligible (years-apart
    /// <see cref="FindCatalogueMatchByDate"/> fallback).
    /// </summary>
    private static Episode? SelectReleaseAlignedTitleMatch(
        Episode probe,
        IEnumerable<Episode> titleMatches,
        Podcast podcast,
        bool requireDayAlignment = false)
    {
        var candidates = titleMatches.ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        if (probe.ReleaseUtc == DateTime.MinValue)
        {
            return requireDayAlignment ? null : candidates.MaxBy(x => x.Title);
        }

        // Unknown catalogue release is only admitted on the non-require path (date-match
        // years-apart fallback). YouTube-discovered enrich must not title-short-circuit on MinValue.
        var dayAligned = candidates
            .Where(x =>
                (!requireDayAlignment && x.ReleaseUtc == DateTime.MinValue) ||
                CalendarDaysWithinSpotifyCatalogueDayTolerance(probe.ReleaseUtc, x.ReleaseUtc, podcast))
            .ToList();

        // Prefer calendar-day alignment so a same-title row three days away cannot beat the
        // same-day row inside the ±14-day submit band. When nothing is day-aligned: either
        // require alignment (YouTube enrich → scorer) or keep unconstrained title containment
        // (audio-to-audio out-of-band years-apart fallback).
        IReadOnlyList<Episode> poolSource;
        if (dayAligned.Count > 0)
        {
            poolSource = dayAligned;
        }
        else if (requireDayAlignment)
        {
            return null;
        }
        else
        {
            poolSource = candidates;
        }

        var probeTitle = EpisodeTitleMatch.Normalise(probe.Title);
        var exact = poolSource
            .Where(x => EpisodeTitleMatch.Normalise(x.Title) == probeTitle)
            .ToList();
        var pool = exact.Count > 0 ? exact : poolSource;

        return pool
            .OrderBy(x => x.ReleaseUtc == DateTime.MinValue
                ? long.MaxValue
                : Math.Abs((x.ReleaseUtc - probe.ReleaseUtc).Ticks))
            .ThenByDescending(x => x.Title, StringComparer.Ordinal)
            .First();
    }
}
