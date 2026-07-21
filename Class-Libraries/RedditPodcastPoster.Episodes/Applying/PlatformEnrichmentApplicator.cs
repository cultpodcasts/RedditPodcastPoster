using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.Episodes.Factories;
using RedditPodcastPoster.Episodes.Merging;
using RedditPodcastPoster.Episodes.Merging.Policies;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Episodes.Applying;

public sealed class PlatformEnrichmentApplicator(
    IEpisodePlatformApplier applier,
    IEpisodeFromCandidateFactory episodeFactory,
    IEnumerable<IReleaseMergePolicy> mergePolicies) : IPlatformEnrichmentApplicator
{
    private readonly IReadOnlyList<IReleaseMergePolicy> _mergePolicies = mergePolicies.ToList();

    public PlatformEnrichmentResult Apply(Podcast podcast, Episode target, EpisodeCandidate candidate)
    {
        if (candidate.SourceLink is not { } link)
        {
            return PlatformEnrichmentResult.None;
        }

        var linkUpdated = applier.ApplyFillMissing(
            target,
            new EpisodePlatformPatch(link, Description: null, Release: null));
        var descriptionUpdated = ApplyDescriptionInternal(target, candidate.Description);

        var incoming = episodeFactory.Create(candidate, target.Explicit);
        var releaseUpdated = ApplyReleaseBackfill(podcast, target, incoming);

        var updated = linkUpdated || descriptionUpdated || releaseUpdated;
        if (!updated)
        {
            return PlatformEnrichmentResult.None;
        }

        return new PlatformEnrichmentResult(
            true,
            link.Service,
            link.Url,
            releaseUpdated,
            releaseUpdated ? target.Release : null);
    }

    public bool ApplyDescription(Episode target, string description) =>
        ApplyDescriptionInternal(target, description);

    public bool ApplySupplementalLink(Episode target, PlatformLink link) =>
        applier.ApplyFillMissing(target, new EpisodePlatformPatch(link, Description: null, Release: null));

    private bool ApplyDescriptionInternal(Episode target, string candidateDescription)
    {
        if (string.IsNullOrWhiteSpace(candidateDescription))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(target.Description))
        {
            target.Description = candidateDescription;
            return true;
        }

        return applier.ApplyFillMissing(
            target,
            new EpisodePlatformPatch(null, candidateDescription, null));
    }

    private bool ApplyReleaseBackfill(Podcast podcast, Episode target, Episode incoming)
    {
        var context = new ReleaseMergeContext(podcast, target, incoming);

        foreach (var policy in _mergePolicies)
        {
            switch (policy.Evaluate(context))
            {
                case ReleaseMergeOpinion.Preserve:
                case ReleaseMergeOpinion.DoNotBackfill:
                    return false;
                case ReleaseMergeOpinion.Backfill:
                    return applier.ApplyFillMissingRelease(target, incoming.Release);
                case ReleaseMergeOpinion.NoOpinion:
                    continue;
            }
        }

        return false;
    }
}
