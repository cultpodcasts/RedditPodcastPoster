using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Matching;

/// <summary>
/// Batch check before a YouTube-discovered episode takes a Spotify or Apple catalogue item (#1445): the hourly
/// indexer enriches one episode at a time, so a weak claimant processed first could take audio that another
/// episode of the same show, still missing that platform, fits far better. The item goes to whichever episode
/// scores highest under <see cref="CatalogueMatchScorer"/>; the claimant yields when a rival beats it and meets
/// the threshold.
/// </summary>
public static class CatalogueCandidateContention
{
    /// <summary>
    /// Returns a rival episode (still missing the platform, per <paramref name="rivalNeedsPlatform"/>) that scores
    /// strictly higher than <paramref name="claimant"/> for <paramref name="candidate"/> and meets the threshold,
    /// or <see langword="null"/> when the claimant keeps it.
    /// </summary>
    public static Episode? FindBetterClaimant(
        Podcast podcast,
        Episode claimant,
        IEnumerable<Episode> episodes,
        Episode candidate,
        Func<Episode, bool> rivalNeedsPlatform)
    {
        var filters = new CatalogueSubjectScoreFilters(podcast.DefaultSubject, podcast.IgnoredSubjects);
        var bestScore = Math.Max(Score(podcast, claimant, candidate, filters), CatalogueMatchScorer.MatchThreshold - 1);
        Episode? best = null;
        foreach (var rival in episodes)
        {
            if (ReferenceEquals(rival, claimant) ||
                rival.Id == claimant.Id ||
                !IsYouTubeDiscovered(rival) ||
                !rivalNeedsPlatform(rival))
            {
                continue;
            }

            var score = Score(podcast, rival, candidate, filters);
            if (score > bestScore)
            {
                best = rival;
                bestScore = score;
            }
        }

        return best;
    }

    private static int Score(
        Podcast podcast,
        Episode episode,
        Episode candidate,
        CatalogueSubjectScoreFilters filters)
    {
        var probe = new Episode
        {
            Title = episode.Title,
            Description = episode.Description,
            Length = episode.Length,
            ReleaseUtc = EpisodeReleaseTolerance.GetAudioReleaseForPlatformLookup(podcast, episode),
            Subjects = episode.Subjects,
            Guests = episode.Guests
        };
        return CatalogueMatchScorer.Score(probe, candidate, filters, episode.ReleaseUtc);
    }

    private static bool IsYouTubeDiscovered(Episode episode) =>
        !string.IsNullOrWhiteSpace(EpisodeServicePresence.YouTubeEpisodeId(episode)) ||
        EpisodeServicePresence.HasUrl(episode, ServiceKeys.YouTube);
}
