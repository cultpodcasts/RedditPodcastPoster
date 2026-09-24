using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Matching.Strategies;

public sealed class ExactReleaseMatchStrategy : IReleaseMatchStrategy
{
    public bool? Evaluate(ReleaseMatchContext context)
    {
        var referenceLength = context.ExistingEpisode.Length > TimeSpan.Zero
            ? context.ExistingEpisode.Length
            : context.IncomingEpisode.Length;
        var toleranceTicks = EpisodeReleaseTolerance.GetToleranceTicks(context.Podcast, referenceLength);
        var delay = context.Podcast.YouTubePublishingDelay();

        if (delay != TimeSpan.Zero &&
            !context.ExistingEpisode.HasYouTubeIdentity() &&
            context.IncomingEpisode.HasYouTubeIdentity())
        {
            var audioRelease = context.ExistingEpisode.ReleaseUtc;
            var youTubeRelease = context.IncomingEpisode.ReleaseUtc;

            if (EpisodeReleaseTolerance.IsYouTubePublishDelayAligned(audioRelease, youTubeRelease, delay))
            {
                return true;
            }

            if (EpisodeReleaseTolerance.AreCrossPlatformReleasesOnSameCalendarDay(audioRelease, youTubeRelease))
            {
                return true;
            }
        }

        if (delay.Ticks >= 0 &&
            Math.Abs((context.ExistingEpisode.ReleaseUtc - context.IncomingEpisode.ReleaseUtc).Ticks) < toleranceTicks)
        {
            return true;
        }

        if (delay == TimeSpan.Zero)
        {
            return false;
        }

        return null;
    }
}
