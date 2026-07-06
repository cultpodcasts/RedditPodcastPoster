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

        // Unreachable when incoming is YouTube: existingIsYouTube == incomingIsYouTube returns false above (line 18).
        // Characterized indirectly via SpotifyCatalogueReleaseMatchStrategy (stored YouTube + incoming Spotify).
        // Test only if strategy registration order changes — see STEP-7-CHECKLIST Phase F P3.
        if (incomingIsYouTube && context.ExistingEpisode.HasSpotifyIdentity() &&
            context.Podcast.ReleaseAuthority == Service.YouTube)
        {
            var expectedYouTubeRelease = context.ExistingEpisode.Release.Add(delay);
            return Math.Abs((context.IncomingEpisode.Release - expectedYouTubeRelease).Ticks) <
                   EpisodeReleaseTolerance.YouTubePublishDelayMatchThreshold.Ticks;
        }

        return false;
    }
}
