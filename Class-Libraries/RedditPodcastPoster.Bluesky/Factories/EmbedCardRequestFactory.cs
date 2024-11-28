using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Bluesky.Client;
using RedditPodcastPoster.Bluesky.YouTube;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.YouTube;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;

namespace RedditPodcastPoster.Bluesky.Factories;

public class EmbedCardRequestFactory(
    ISpotifyEpisodeResolver spotifyEpisodeResolver,
    IBlueskyYouTubeServiceWrapper youTubeService,
    IYouTubeVideoService youTubeVideoService,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<EmbedCardRequestFactory> logger
#pragma warning restore CS9113 // Parameter is unread.
) : IEmbedCardRequestFactory
{
    public async Task<EmbedCardRequest?> CreateEmbedCardRequest(PodcastEpisode podcastEpisode,
        BlueskyEmbedCardPost embedPost)
    {
        EmbedCardRequest? embedCardRequest = null;
        var indexingContext = new IndexingContext();
        switch (embedPost.UrlService)
        {
            case Service.YouTube:
            {
                var video = await youTubeVideoService.GetVideoContentDetails(
                    youTubeService,
                    [podcastEpisode.Episode.YouTubeId],
                    indexingContext, true);
                if (video?.FirstOrDefault() != null)
                {
                    embedCardRequest = new EmbedCardRequest(
                        video.First().Snippet.Title.Trim(),
                        video.First().Snippet.Description.Trim(),
                        embedPost.Url)
                    {
                        ThumbUrl = video.FirstOrDefault().GetImageUrl()
                    };
                    logger.LogInformation(
                        $"Youtube-thumbnail for youtube-video-id '{podcastEpisode.Episode.YouTubeId}' found '{embedCardRequest.ThumbUrl}'.");
                }
                else
                {
                    logger.LogError(
                        $"Unable to find video for bluesky-thumbnail with id '{podcastEpisode.Episode.YouTubeId}'. {nameof(indexingContext.SkipYouTubeUrlResolving)}= '{indexingContext.SkipYouTubeUrlResolving}'.");
                }

                break;
            }
            case Service.Spotify:
            {
                var episode = await spotifyEpisodeResolver.FindEpisode(
                    FindSpotifyEpisodeRequestFactory.Create(podcastEpisode.Podcast, podcastEpisode.Episode),
                    indexingContext);
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
                        logger.LogInformation(
                            $"Spotify-thumbnail for spotify-episode-id '{podcastEpisode.Episode.SpotifyId}' found '{embedCardRequest.ThumbUrl}'.");
                    }
                }

                break;
            }
        }

        return embedCardRequest;
    }
}