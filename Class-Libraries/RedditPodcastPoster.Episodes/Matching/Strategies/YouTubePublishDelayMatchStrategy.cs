using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Episodes.Matching.Strategies;

public sealed class YouTubePublishDelayMatchStrategy : IReleaseMatchStrategy
{
    public bool? Evaluate(ReleaseMatchContext context)
    {
        var delay = context.Podcast.YouTubePublishingDelay();
        if (delay == TimeSpan.Zero)
        {
            return null;
        }

        var existingIsYouTube = context.ExistingEpisode.HasYouTubeIdentity();
        var incomingIsYouTube = context.IncomingEpisode.HasYouTubeIdentity();
        if (existingIsYouTube == incomingIsYouTube)
        {
            return null;
        }

        if (!existingIsYouTube && incomingIsYouTube)
        {
            return EpisodeReleaseTolerance.IsYouTubePublishDelayAligned(
                context.ExistingEpisode.Release,
                context.IncomingEpisode.Release,
                delay)
                ? true
                : null;
        }

        return null;
    }
}
