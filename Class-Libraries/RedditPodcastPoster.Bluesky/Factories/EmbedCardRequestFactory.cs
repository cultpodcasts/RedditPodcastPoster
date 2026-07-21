using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Bluesky.Client;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Bluesky.YouTube;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Factories;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;

namespace RedditPodcastPoster.Bluesky.Factories;

public class EmbedCardRequestFactory(
    ISpotifyEpisodeResolver spotifyEpisodeResolver,
    IBlueskyYouTubeServiceWrapper youTubeService,
    IYouTubeVideoService youTubeVideoService,
    IYouTubeThumbnailResolver youTubeThumbnailResolver,
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
                        ThumbUrl = await youTubeThumbnailResolver.GetImageUrlAsync(video.First())
                    };
                    logger.LogInformation(
                        "Youtube-thumbnail for youtube-video-id '{EpisodeYouTubeId}' found '{ThumbUrl}'.", podcastEpisode.Episode.YouTubeId, embedCardRequest.ThumbUrl);
                }
                else
                {
                    logger.LogError(
                        "Unable to find video for bluesky-thumbnail with id '{EpisodeYouTubeId}'. {IndexingContextSkipYouTubeUrlResolvingName}= '{IndexingContextSkipYouTubeUrlResolving}'.", podcastEpisode.Episode.YouTubeId, nameof(indexingContext.SkipYouTubeUrlResolving), indexingContext.SkipYouTubeUrlResolving);
                    throw new EpisodeNotFoundException(podcastEpisode.Episode.YouTubeId, Service.YouTube);
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
                    var maxImage = episode.FullEpisode.GetBestImageUrl();
                    if (maxImage != null)
                    {
                        embedCardRequest.ThumbUrl = maxImage;
                        logger.LogInformation(
                            "Spotify-thumbnail for spotify-episode-id '{EpisodeSpotifyId}' found '{ThumbUrl}'.", podcastEpisode.Episode.SpotifyId, embedCardRequest.ThumbUrl);
                    }
                }

                break;
            }
            case Service.Other:
            {
                embedCardRequest = new EmbedCardRequest(
                    podcastEpisode.Episode.Title,
                    podcastEpisode.Episode.Description,
                    embedPost.Url);
                if (podcastEpisode.Episode.Images?.Other != null)
                {
                    embedCardRequest.ThumbUrl = podcastEpisode.Episode.Images?.Other;
                }

                break;
            }
        }

        return embedCardRequest;
    }
}
