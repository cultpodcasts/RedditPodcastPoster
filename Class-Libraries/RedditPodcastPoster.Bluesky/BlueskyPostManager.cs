using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
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
            unposted = await podcastEpisodeProvider.GetBlueskyReadyPodcastEpisodes(youTubeRefreshed, spotifyRefreshed);
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

                    logger.LogInformation("Bluesky Post init.");
                    try
                    {
                        var status = await poster.Post(podcastEpisode, shortnerResult.Url);
                        logger.LogInformation("Bluesky Post complete. Bluesky-post-status: '{status}'.", status);
                        posted = status == BlueskySendStatus.Success;
                    }
                    catch (EpisodeNotFoundException e)
                    {
                        logger.LogError(e, "Candidate episode to post to bluesky not found");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Unable to bluesky-post episode with id '{episodeId}' with title '{episodeTitle}' from podcast with id '{podcastId}' and name '{podcastName}'.",
                        podcastEpisode.Episode.Id, podcastEpisode.Episode.Title, podcastEpisode.Podcast.Id,
                        podcastEpisode.Podcast.Name);
                }
            }
        }
    }
}