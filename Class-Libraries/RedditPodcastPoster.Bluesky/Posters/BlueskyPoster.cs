using System.Security.Authentication;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Bluesky.Client;
using RedditPodcastPoster.Bluesky.Factories;
using RedditPodcastPoster.Bluesky.Logging;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Bluesky.Posters;

public class BlueskyPoster(
    IEpisodeRepository episodeRepository,
    IBlueskyEmbedCardPostFactory embedCardPostFactory,
    IBlueskyFeedClient blueSkyClient,
    ILogger<BlueskyPoster> logger)
    : IBlueskyPoster
{
    public async Task<BlueskySendStatus> Post(PodcastEpisode podcastEpisode, Uri? shortUrl, bool hasShareImage = false)
    {
        var embedPost = await embedCardPostFactory.Create(podcastEpisode, shortUrl, hasShareImage);
        BlueskySendStatus sendStatus;
        var language = string.IsNullOrWhiteSpace(podcastEpisode.Episode.Language)
            ? "en"
            : podcastEpisode.Episode.Language.Trim();
        string? blueskyPostUri;
        try
        {
            logger.LogInformation(
                "Posting bluesky open-graph card for episode '{podcastEpisodeId}' at '{embedPostUrl}'.",
                podcastEpisode.Episode.Id,
                embedPost.Url);
            blueskyPostUri = await blueSkyClient.PostOpenGraphCard(embedPost.Text, embedPost.Url, language);
            if (string.IsNullOrWhiteSpace(blueskyPostUri))
            {
                return BlueskySendStatus.Failure;
            }

            sendStatus = BlueskySendStatus.Success;
            BlueskyPostLogger.LogPosted(
                logger,
                podcastEpisode,
                caller: nameof(BlueskyPoster) + "." + nameof(Post));
            logger.LogInformation(
                "Bluesky post AT URI: {BlueskyPostUri}. Episode-id: {EpisodeId}.",
                blueskyPostUri,
                podcastEpisode.Episode.Id);
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

        podcastEpisode.Episode.BlueskyPost = blueskyPostUri;
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
