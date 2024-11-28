using System.Security.Authentication;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Bluesky.Client;
using RedditPodcastPoster.Bluesky.Factories;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Bluesky;

public class BlueskyPoster(
    IPodcastRepository repository,
    IBlueskyEmbedCardPostFactory embedCardPostFactory,
    IEmbedCardBlueskyClient blueSkyClient,
    IEmbedCardRequestFactory embedCardRequestFactory,
    ILogger<BlueskyPoster> logger)
    : IBlueskyPoster
{
    public async Task<BlueskySendStatus> Post(PodcastEpisode podcastEpisode, Uri? shortUrl)
    {
        var embedPost = await embedCardPostFactory.BuildPost(podcastEpisode, shortUrl);
        BlueskySendStatus sendStatus;
        var embedCardRequest = await embedCardRequestFactory.CreateEmbedCardRequest(podcastEpisode, embedPost);
        try
        {
            if (embedCardRequest != null)
            {
                logger.LogInformation($"Non-Null {nameof(EmbedCardRequest)} for episode with id '{podcastEpisode.Episode.Id}'.");
                await blueSkyClient.Post(embedPost.Text, embedCardRequest);
            }
            else
            {
                logger.LogError($"Null {nameof(EmbedCardRequest)} for episode with id '{podcastEpisode.Episode.Id}'.");
                await blueSkyClient.Post($"{embedPost.Text}{Environment.NewLine}{embedPost.Url}");
            }

            sendStatus = BlueskySendStatus.Success;
            logger.LogInformation($"Posted to bluesky: '{embedPost.Text}'.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex,
                $"Failure making http-request sending blue-sky post for podcast-id '{podcastEpisode.Podcast.Id}' episode-id '{podcastEpisode.Episode.Id}'. Status-code: '{ex.StatusCode}', request-error: '{ex.HttpRequestError}'. Post: '{embedPost.Text}'.");
            return BlueskySendStatus.FailureHttp;
        }
        catch (AuthenticationException ex)
        {
            logger.LogError(ex,
                $"Failure authenticating to send blue-sky post. Post: '{embedPost.Text}'.");
            return BlueskySendStatus.FailureAuth;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                $"Failure to send blue-sky post for podcast-id '{podcastEpisode.Podcast.Id}' episode-id '{podcastEpisode.Episode.Id}', post: '{embedPost.Text}'.");
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