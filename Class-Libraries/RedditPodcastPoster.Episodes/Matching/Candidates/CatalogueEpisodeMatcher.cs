using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Matching.Candidates;

/// <summary>
/// One matching step for every audio platform:
/// <list type="number">
/// <item>With a release date, candidates inside the submit match band are tried before the rest, so a
/// years-apart episode with a similar title only wins when nothing in the band does.</item>
/// <item>Within each slice, <see cref="CatalogueLookup.UsesLengthStrategy"/> chooses
/// <see cref="IEpisodePlatformMatcher.FindCatalogueMatchByLength"/>; otherwise
/// <see cref="IEpisodePlatformMatcher.FindCatalogueMatchByDate"/>.</item>
/// </list>
/// </summary>
public sealed class CatalogueEpisodeMatcher(IEpisodePlatformMatcher platformMatcher) : ICatalogueEpisodeMatcher
{
    public Episode? Match(
        CatalogueLookup lookup,
        Episode probe,
        IReadOnlyList<Episode> candidates,
        IReadOnlyList<string>? ignoredSubjects,
        Func<Episode, bool>? reducer = null)
    {
        if (lookup.Released is not { } released)
        {
            return MatchSlice(lookup, probe, candidates, ignoredSubjects, reducer);
        }

        var inBand = candidates
            .Where(x => EpisodeReleaseTolerance.IsInSubmitMatchBand(x.ReleaseUtc, released))
            .ToList();
        var outOfBand = candidates.Except(inBand).ToList();
        return MatchSlice(lookup, probe, inBand, ignoredSubjects, reducer) ??
               MatchSlice(lookup, probe, outOfBand, ignoredSubjects, reducer);
    }

    private Episode? MatchSlice(
        CatalogueLookup lookup,
        Episode probe,
        IReadOnlyList<Episode> candidates,
        IReadOnlyList<string>? ignoredSubjects,
        Func<Episode, bool>? reducer)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        var podcast = new Podcast
        {
            ReleaseAuthority = lookup.ReleaseAuthority,
            YouTubePublicationOffset = lookup.YouTubePublishingDelay?.Ticks
        };

        if (lookup.UsesLengthStrategy)
        {
            return platformMatcher.FindCatalogueMatchByLength(
                probe,
                candidates,
                podcast,
                episodeMatchRegex: null,
                new CatalogueMatchByLengthOptions(
                    ReleaseAuthority: lookup.ReleaseAuthority,
                    AcceptUniqueDurationWithoutTitleMatch: false,
                    EnrichingYouTubeDiscoveredEpisode: lookup.EnrichingYouTubeDiscoveredEpisode,
                    DefaultSubject: lookup.DefaultSubject,
                    IgnoredSubjects: ignoredSubjects),
                reducer);
        }

        return platformMatcher.FindCatalogueMatchByDate(probe, candidates, podcast, episodeMatchRegex: null, reducer);
    }
}
