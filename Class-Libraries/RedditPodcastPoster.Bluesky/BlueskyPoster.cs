using System.Security.Authentication;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Bluesky.Factories;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using X.Bluesky;
using X.Bluesky.Models;

namespace RedditPodcastPoster.Bluesky;

public class BlueskyPoster(
    IPodcastRepository repository,
    IBlueskyEmbedCardPostFactory embedCardPostFactory,
    IBlueskyClient blueSkyClient,
    ILogger<BlueskyPoster> logger)
    : IBlueskyPoster
{
    public async Task<BlueskySendStatus> Post(PodcastEpisode podcastEpisode, Uri? shortUrl)
    {
        var embedPost = await embedCardPostFactory.Create(podcastEpisode, shortUrl);
        var post = embedPost.Images == null
            ? new Post
            {
                Text = embedPost.Text,
                Url = embedPost.Url,
                GenerateCardForUrl = true
            }
            : new Post
            {
                Text = embedPost.Text,
                Url = embedPost.Url,
                GenerateCardForUrl = true,
                Images = embedPost.Images
            };

        BlueskySendStatus sendStatus;
        try
        {
            await blueSkyClient.Post(post);
            sendStatus = BlueskySendStatus.Success;
            logger.LogInformation($"Posted to bluesky: '{embedPost.Text}'.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex,
                "Failure making http-request sending blue-sky post for podcast-id '{podcastId}' episode-id '{episodeId}'. Status-code: '{statusCode}', request-error: '{httpRequestError}'. Post: '{embedPostText}'.",
                podcastEpisode.Podcast.Id, podcastEpisode.Episode.Id, ex.StatusCode, ex.HttpRequestError,
                embedPost.Text);
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
                "Failure to send blue-sky post for podcast-id '{podcast.Id}' episode-id '{episodeId}', post: '{embedPostText}'.",
                podcastEpisode.Podcast.Id, podcastEpisode.Episode.Id, embedPost.Text);
            return BlueskySendStatus.Failure;
        }

        podcastEpisode.Episode.BlueskyPosted = true;
        try
        {
            await repository.Save(podcastEpisode.Podcast);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failure to save podcast with podcast-id '{podcast.Id}' to update episode with id '{episodeId}'.",
                podcastEpisode.Podcast.Id, podcastEpisode.Episode.Id);
            throw;
        }

        return sendStatus;
    }
}