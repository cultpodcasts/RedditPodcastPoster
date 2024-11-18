using System.Security.Authentication;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Bluesky.Client;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.YouTube;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;

namespace RedditPodcastPoster.Bluesky;

public class BlueskyPoster(
    IPodcastRepository repository,
    IBlueskyPostBuilder postBuilder,
    IEmbedCardBlueskyClient blueSkyClient,
    IYouTubeVideoService youTubeVideoService,
    ISpotifyEpisodeResolver spotifyEpisodeResolver,
    ILogger<BlueskyPoster> logger)
    : IBlueskyPoster
{
    public async Task<BlueskySendStatus> Post(PodcastEpisode podcastEpisode, Uri? shortUrl)
    {
        var embedPost = await postBuilder.BuildPost(podcastEpisode, shortUrl);
        BlueskySendStatus sendStatus;
        try
        {
            EmbedCardRequest? embedCardRequest = null;
            switch (embedPost.UrlService)
            {
                case Service.YouTube:
                {
                    var video = await youTubeVideoService.GetVideoContentDetails(
                        [podcastEpisode.Episode.YouTubeId],
                        new IndexingContext(), true);
                    if (video?.FirstOrDefault() != null)
                    {
                        embedCardRequest = new EmbedCardRequest(
                            video.First().Snippet.Title.Trim(),
                            video.First().Snippet.Description.Trim(),
                            embedPost.Url)
                        {
                            ThumbUrl = video.FirstOrDefault().GetImageUrl()
                        };
                    }

                    break;
                }
                case Service.Spotify:
                {
                    var episode = await spotifyEpisodeResolver.FindEpisode(
                        FindSpotifyEpisodeRequestFactory.Create(podcastEpisode.Podcast, podcastEpisode.Episode),
                        new IndexingContext());
                    if (episode.FullEpisode != null)
                    {
                        embedCardRequest = new EmbedCardRequest(
                            episode.FullEpisode.Name,
                            episode.FullEpisode.Description,
                            embedPost.Url);
                        var maxImage = episode.FullEpisode.Images.MaxBy(x => x.Height);
                        if (maxImage != null)
                        {
                            embedCardRequest.ThumbUrl = new Uri(maxImage.Url);
                        }
                    }

                    break;
                }
            }

            if (embedCardRequest != null)
            {
                await blueSkyClient.Post(embedPost.Text, embedCardRequest);
            }
            else
            {
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