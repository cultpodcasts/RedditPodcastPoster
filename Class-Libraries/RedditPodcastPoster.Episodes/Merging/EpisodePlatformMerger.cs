using RedditPodcastPoster.Episodes.Applying;
using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Episodes.Merging;

public sealed class EpisodePlatformMerger(
    IEpisodePlatformApplier applier,
    IEnumerable<IReleaseMergePolicy> mergePolicies) : IEpisodePlatformMerger
{
    private readonly IReadOnlyList<IReleaseMergePolicy> _mergePolicies = mergePolicies.ToList();

    public bool MergeInPlace(Episode existingEpisode, Episode incomingEpisode, Podcast podcast)
    {
        var updated = false;

        updated |= applier.ApplyFillMissing(existingEpisode, incomingEpisode.ToSpotifyPatch());
        updated |= applier.ApplyFillMissing(existingEpisode, incomingEpisode.ToApplePatch());
        updated |= applier.ApplyFillMissing(existingEpisode, incomingEpisode.ToYouTubePatch());

        updated |= ApplyReleaseMerge(existingEpisode, incomingEpisode, podcast);

        return updated;
    }

    private bool ApplyReleaseMerge(Episode existingEpisode, Episode incomingEpisode, Podcast podcast)
    {
        var context = new ReleaseMergeContext(podcast, existingEpisode, incomingEpisode);

        foreach (var policy in _mergePolicies)
        {
            switch (policy.Evaluate(context))
            {
                case ReleaseMergeOpinion.Preserve:
                case ReleaseMergeOpinion.DoNotBackfill:
                    return false;
                case ReleaseMergeOpinion.Backfill:
                    return applier.ApplyFillMissingRelease(
                        existingEpisode,
                        incomingEpisode.Release);
                case ReleaseMergeOpinion.NoOpinion:
                    continue;
            }
        }

        return false;
    }
}
