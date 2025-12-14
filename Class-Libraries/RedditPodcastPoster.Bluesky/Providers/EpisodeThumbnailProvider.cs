using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Bluesky.YouTube;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Factories;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Video;

namespace RedditPodcastPoster.Bluesky.Providers;

public class EpisodeThumbnailProvider(
    ISpotifyEpisodeResolver spotifyEpisodeResolver,
    IBlueskyYouTubeServiceWrapper youTubeService,
    IYouTubeVideoService youTubeVideoService,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<EpisodeThumbnailProvider> logger
#pragma warning restore CS9113 // Parameter is unread.
) : IEpisodeThumbnailProvider
{
    public async Task<Uri?> GetThumbnail(
        PodcastEpisode podcastEpisode,
        Service urlService)
    {
        Uri? imageUrl = null;
        var indexingContext = new IndexingContext();
        switch (urlService)
        {
            case Service.YouTube:
            {
                var video = await youTubeVideoService.GetVideoContentDetails(
                    youTubeService,
                    [podcastEpisode.Episode.YouTubeId],
                    indexingContext, true);
                if (video?.FirstOrDefault() != null)
                {
                    imageUrl = video.FirstOrDefault().GetImageUrl();
                    logger.LogInformation(
                        $"Youtube-thumbnail for youtube-video-id '{podcastEpisode.Episode.YouTubeId}' found '{imageUrl}'.");
                }
                else
                {
                    logger.LogError(
                        $"Unable to find video for bluesky-thumbnail with id '{podcastEpisode.Episode.YouTubeId}'. {nameof(indexingContext.SkipYouTubeUrlResolving)}= '{indexingContext.SkipYouTubeUrlResolving}'.");
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
                    var maxImage = episode.FullEpisode.GetBestImageUrl();
                    if (maxImage != null)
                    {
                        imageUrl = maxImage;
                        logger.LogInformation(
                            $"Spotify-thumbnail for spotify-episode-id '{podcastEpisode.Episode.SpotifyId}' found '{imageUrl}'.");
                    }
                }

                break;
            }
            case Service.Other:
            {
                if (podcastEpisode.Episode.Images?.Other != null)
                {
                    imageUrl = podcastEpisode.Episode.Images?.Other;
                    logger.LogInformation(
                        $"Other-thumbnail for episode-id '{podcastEpisode.Episode.Id}' found '{imageUrl}'.");
                }

                break;
            }
        }

        return imageUrl;
    }
}