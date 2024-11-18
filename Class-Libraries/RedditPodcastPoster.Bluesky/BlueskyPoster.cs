using System.Security.Authentication;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.YouTube;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using X.Bluesky;

namespace RedditPodcastPoster.Bluesky;

public class BlueskyPoster(
    IPodcastRepository repository,
    IBlueskyPostBuilder postBuilder,
    IBlueskyClient blueSkyClient,
    IYouTubeVideoService youTubeVideoService,
    ISpotifyEpisodeResolver spotifyEpisodeResolver,
    ILogger<BlueskyPoster> logger)
    : IBlueskyPoster
{
    public async Task<BlueskySendStatus> Post(PodcastEpisode podcastEpisode, Uri? shortUrl)
    {
        var (post, url, service) = await postBuilder.BuildPost(podcastEpisode, shortUrl);
        BlueskySendStatus sendStatus;
        try
        {
            logger.LogInformation($"bluesky-post-url: {url}");
            if (service == Service.YouTube)
            {
                var video = await youTubeVideoService.GetVideoContentDetails([podcastEpisode.Episode.YouTubeId],
                    new IndexingContext(), true);
                EmbedCardRequest embedCardRequest;
                if (video?.FirstOrDefault() == null)
                {
                    embedCardRequest = new EmbedCardRequest(podcastEpisode.Episode.Title,
                        podcastEpisode.Episode.Description, url);
                }
                else
                {
                    embedCardRequest = new EmbedCardRequest(video.First().Snippet.Title.Trim(),
                        video.First().Snippet.Description.Trim(), url)
                    {
                        ThumbUrl = video.FirstOrDefault().GetImageUrl()
                    };
                }

                await blueSkyClient.Post(post, embedCardRequest);
            }
            else if (service == Service.Spotify)
            {
                var episode = await spotifyEpisodeResolver.FindEpisode(
                    FindSpotifyEpisodeRequestFactory.Create(podcastEpisode.Podcast, podcastEpisode.Episode),
                    new IndexingContext());
                EmbedCardRequest embedCardRequest;
                if (episode.FullEpisode != null)
                {
                    embedCardRequest =
                        new EmbedCardRequest(episode.FullEpisode.Name, episode.FullEpisode.Description, url);
                    var maxImage = episode.FullEpisode.Images.MaxBy(x => x.Height);
                    if (maxImage != null)
                    {
                        embedCardRequest.ThumbUrl = new Uri(maxImage.Url);
                    }
                }
                else
                {
                    embedCardRequest = new EmbedCardRequest(podcastEpisode.Episode.Title,
                        podcastEpisode.Episode.Description, url);
                }

                await blueSkyClient.Post(post, embedCardRequest);
            }
            else
            {
                await blueSkyClient.Post(post, url);
            }

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