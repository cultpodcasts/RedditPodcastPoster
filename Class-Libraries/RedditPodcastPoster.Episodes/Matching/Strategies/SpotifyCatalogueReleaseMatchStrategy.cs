using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Matching.Strategies;

public sealed class SpotifyCatalogueReleaseMatchStrategy : IReleaseMatchStrategy
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

        var referenceLength = context.ExistingEpisode.Length > TimeSpan.Zero
            ? context.ExistingEpisode.Length
            : context.IncomingEpisode.Length;
        var toleranceTicks = EpisodeReleaseTolerance.GetToleranceTicks(context.Podcast, referenceLength);

        if (existingIsYouTube && context.IncomingEpisode.HasSpotifyIdentity() &&
            context.Podcast.ReleaseAuthority == Service.YouTube)
        {
            var expectedAudioRelease = context.ExistingEpisode.Release - delay;
            return EpisodeReleaseTolerance.AudioCatalogueReleaseMatches(
                context.IncomingEpisode.Release,
                expectedAudioRelease,
                toleranceTicks,
                context.Podcast);
        }

        return null;
    }
}
