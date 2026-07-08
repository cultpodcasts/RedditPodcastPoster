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
            return false;
        }

        if (!existingIsYouTube && incomingIsYouTube)
        {
            var expectedPublish = context.ExistingEpisode.Release.Add(delay);
            return Math.Abs((context.IncomingEpisode.Release - expectedPublish).Ticks) <
                   EpisodeReleaseTolerance.YouTubePublishDelayMatchThreshold.Ticks;
        }

        return false;
    }
}
