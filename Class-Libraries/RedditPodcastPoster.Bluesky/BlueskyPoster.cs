using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using X.Bluesky;

namespace RedditPodcastPoster.Bluesky;

public class BlueskyPoster(
    IPodcastRepository repository,
    IBlueskyPostBuilder postBuilder,
    IBlueskyClient blueSkyClient,
    ILogger<BlueskyPoster> logger)
    : IBlueskyPoster
{
    public async Task<BlueskySendStatus> Post(PodcastEpisode podcastEpisode, Uri? shortUrl)
    {
        var (post, url) = await postBuilder.BuildPost(podcastEpisode, shortUrl);
        BlueskySendStatus sendStatus;
        try
        {
            await blueSkyClient.Post(post, url);
            sendStatus = BlueskySendStatus.Success;
            logger.LogInformation($"Posted to bluesky: '{post}'.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                $"Failure to send post for podcast-id '{podcastEpisode.Podcast.Id}' episode-id '{podcastEpisode.Episode.Id}', post]: '{post}'.");
            throw;
        }

        podcastEpisode.Episode.BlueskyPosted = true;
        try
        {
            await repository.Update(podcastEpisode.Podcast);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                $"Failure to save podcast with podcast-id '{podcastEpisode.Podcast.Id}' to update episode with id '{podcastEpisode.Episode.Id}'.");
            throw;
        }


        return sendStatus;
    }
}