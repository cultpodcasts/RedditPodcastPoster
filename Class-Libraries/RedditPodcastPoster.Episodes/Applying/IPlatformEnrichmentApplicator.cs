using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Episodes.Applying;

public interface IPlatformEnrichmentApplicator
{
    PlatformEnrichmentResult Apply(Podcast podcast, Episode target, EpisodeCandidate candidate);

    bool ApplyDescription(Episode target, string description);

    bool ApplySupplementalLink(Episode target, PlatformLink link);
}
