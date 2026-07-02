using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Episodes.Merging.Policies;

public sealed class YouTubeAuthoritativePreserveMergePolicy : IReleaseMergePolicy
{
    public ReleaseMergeOpinion Evaluate(ReleaseMergeContext context)
    {
        if (context.Podcast.ReleaseAuthority == Service.YouTube &&
            context.ExistingEpisode.HasYouTubeIdentity())
        {
            return ReleaseMergeOpinion.Preserve;
        }

        return ReleaseMergeOpinion.NoOpinion;
    }
}
