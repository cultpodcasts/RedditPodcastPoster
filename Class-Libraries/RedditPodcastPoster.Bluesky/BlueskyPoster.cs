using System.Security.Authentication;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Bluesky.Client;
using RedditPodcastPoster.Bluesky.Factories;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Bluesky;

public class BlueskyPoster(
    IEpisodeRepository episodeRepository,
    IBlueskyEmbedCardPostFactory embedCardPostFactory,
    IEmbedCardBlueskyClient blueSkyClient,
    IEmbedCardRequestFactory embedCardRequestFactory,
    ILogger<BlueskyPoster> logger)
    : IBlueskyPoster
{
    public async Task<BlueskySendStatus> Post(PodcastEpisodeV2 podcastEpisode, Uri? shortUrl)
    {
        var embedPost = await embedCardPostFactory.Create(podcastEpisode, shortUrl);
        BlueskySendStatus sendStatus;
        var embedCardRequest = await embedCardRequestFactory.CreateEmbedCardRequest(podcastEpisode, embedPost);
        try
        {
            if (embedCardRequest != null)
            {
                logger.LogInformation(
                    "Non-Null {nameofEmbedCardRequest} for episode with id '{podcastEpisodeId}'.",
                    nameof(EmbedCardRequest), podcastEpisode.Episode.Id);
                await blueSkyClient.Post(embedPost.Text, embedCardRequest);
            }
            else
            {
                logger.LogError("Null {nameofEmbedCardRequest} for episode with id '{podcastEpisodeId}'.",
                    nameof(EmbedCardRequest), podcastEpisode.Episode.Id);
                await blueSkyClient.Post($"{embedPost.Text}{Environment.NewLine}{embedPost.Url}");
            }

            sendStatus = BlueskySendStatus.Success;
            logger.LogInformation("Posted to bluesky: '{EmbedPostText}'.", embedPost.Text);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex,
                "Failure making http-request sending blue-sky post for podcast-id '{podcastId}' episode-id '{episodeId}'. Status-code: '{statusCode}', request-error: '{httpRequestError}'. Post: '{embedPostText}', Url: '{embedPostUrl}'.",
                podcastEpisode.Podcast.Id, podcastEpisode.Episode.Id, ex.StatusCode, ex.HttpRequestError,
                embedPost.Text, embedPost.Url);
            return BlueskySendStatus.FailureHttp;
        }
        catch (AuthenticationException ex)
        {
            logger.LogError(ex,
                "Failure authenticating to send blue-sky post. Post: '{embedPostText}'.", embedPost.Text);
            return BlueskySendStatus.FailureAuth;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failure to send blue-sky post for podcast-id '{podcastId}' episode-id '{episodeId}', post: '{embedPostText}'.",
                podcastEpisode.Podcast.Id, podcastEpisode.Episode.Id, embedPost.Text);
            return BlueskySendStatus.Failure;
        }

        podcastEpisode.Episode.BlueskyPosted = true;
        try
        {
            await episodeRepository.Save(podcastEpisode.Episode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failure to save episode with podcast-id '{PodcastId}' and episode-id '{EpisodeId}' after bluesky update.",
                podcastEpisode.Podcast.Id, podcastEpisode.Episode.Id);
            throw;
        }

        return sendStatus;
    }
}