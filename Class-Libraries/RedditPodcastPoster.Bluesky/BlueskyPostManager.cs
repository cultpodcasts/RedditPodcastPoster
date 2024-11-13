using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.UrlShortening;

namespace RedditPodcastPoster.Bluesky;

public class BlueskyPostManager(
    IBlueskyPoster poster,
    IPodcastEpisodeProvider podcastEpisodeProvider,
    IShortnerService shortnerService,
    ILogger<BlueskyPostManager> logger)
    : IBlueskyPostManager
{
    public async Task Post(
        bool youTubeRefreshed,
        bool spotifyRefreshed)
    {
        IEnumerable<PodcastEpisode> unposted;
        try
        {
            logger.LogInformation(
                $"{nameof(BlueskyPostManager)}.{nameof(Post)}: Exec {nameof(podcastEpisodeProvider.GetBlueskyReadyPodcastEpisodes)} init.");
            unposted = await podcastEpisodeProvider.GetBlueskyReadyPodcastEpisodes(youTubeRefreshed, spotifyRefreshed);
            logger.LogInformation(
                $"{nameof(BlueskyPostManager)}.{nameof(Post)}: Exec {nameof(podcastEpisodeProvider.GetBlueskyReadyPodcastEpisodes)} complete.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure to find podcast-episode.");
            throw;
        }

        if (unposted.Any())
        {
            var posted = false;
            foreach (var podcastEpisode in unposted)
            {
                if (posted)
                {
                    break;
                }

                try
                {
                    var shortnerResult = await shortnerService.Write(podcastEpisode);
                    if (!shortnerResult.Success)
                    {
                        logger.LogError("Unsuccessful shortening-url.");
                    }

                    logger.LogInformation(
                        $"{nameof(BlueskyPostManager)}.{nameof(Post)}: Exec {nameof(poster.Post)} init.");
                    var tweetStatus = await poster.Post(podcastEpisode, shortnerResult.Url);
                    logger.LogInformation(
                        $"{nameof(BlueskyPostManager)}.{nameof(Post)}: Exec {nameof(poster.Post)} complete. Tweet-status: '{tweetStatus}'.");
                    posted = tweetStatus == BlueskySendStatus.Success;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        $"Unable to bluesky-post episode with id '{podcastEpisode.Episode.Id}' with title '{podcastEpisode.Episode.Title}' from podcast with id '{podcastEpisode.Podcast.Id}' and name '{podcastEpisode.Podcast.Name}'.");
                }
            }
        }
    }
}