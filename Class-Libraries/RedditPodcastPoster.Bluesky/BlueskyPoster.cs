using System.Security.Authentication;
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
        catch (HttpRequestException ex)
        {
            logger.LogError(ex,
                $"Failure making http-request sending blue-sky post for podcast-id '{podcastEpisode.Podcast.Id}' episode-id '{podcastEpisode.Episode.Id}'. Status-code: '{ex.StatusCode}', request-error: '{ex.HttpRequestError}'. Post: '{post}'.");
            return BlueskySendStatus.FailureHttp;
        }
        catch (AuthenticationException ex)
        {
            logger.LogError(ex,
                $"Failure authenticating to send blue-sky post. Post: '{post}'.");
            return BlueskySendStatus.FailureAuth;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                $"Failure to send blue-sky post for podcast-id '{podcastEpisode.Podcast.Id}' episode-id '{podcastEpisode.Episode.Id}', post: '{post}'.");
            return BlueskySendStatus.Failure;
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