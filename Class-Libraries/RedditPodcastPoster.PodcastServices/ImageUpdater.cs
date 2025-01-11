using Microsoft.Extensions.Logging;
using RedditPodcastPoster.BBC;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Video;

namespace RedditPodcastPoster.PodcastServices;

public class ImageUpdater(
    ISpotifyEpisodeResolver spotifyEpisodeResolver,
    IAppleEpisodeResolver appleEpisodeResolver,
    IYouTubeServiceWrapper youTubeService,
    IYouTubeVideoService youTubeVideoService,
    IBBCPageMetaDataExtractor bbcPageMetaDataExtractor,
    ILogger<ImageUpdater> logger) : IImageUpdater
{
    public async Task<bool> UpdateImages(Podcast podcast, Episode episode, EpisodeImageUpdateRequest updateRequest, IndexingContext indexingContext)
    {
        var updated = false;
        if (updateRequest.UpdateSpotifyImage == true && !string.IsNullOrWhiteSpace(episode.SpotifyId))
        {
            try
            {
                var fullEpisode = await spotifyEpisodeResolver.FindEpisode(
                    FindSpotifyEpisodeRequestFactory.Create(episode.SpotifyId),
                    indexingContext);
                if (fullEpisode != null)
                {
                    episode.Images ??= new EpisodeImages();
                    episode.Images.Spotify = fullEpisode.FullEpisode?.GetBestImageUrl();
                    updated = true;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failure updating image for episode with id '{episodeId}' and spotify-id '{spotifyId}'.",
                    episode.Id,
                    episode.SpotifyId);
            }
        }
        if (updateRequest.UpdateAppleImage == true && episode.AppleId != null && podcast.AppleId != null)
        {
            try
            {
                var appleEpisode = await appleEpisodeResolver.FindEpisode(
                    FindAppleEpisodeRequestFactory.Create(podcast, episode),
                    indexingContext);
                if (appleEpisode != null)
                {
                    episode.Images ??= new EpisodeImages();
                    episode.Images.Apple = appleEpisode.Image;
                    updated = true;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failure updating image for episode with id '{episodeId}' and apple-id '{appleId}'.",
                    episode.Id,
                    episode.AppleId);
            }

        }
        if (updateRequest.UpdateYouTubeImage == true && !string.IsNullOrWhiteSpace(episode.YouTubeId))
        {
            try
            {
                var video = await youTubeVideoService.GetVideoContentDetails(youTubeService, [episode.YouTubeId], indexingContext, withSnippets: true);
                if (video != null && video.Any())
                {
                    episode.Images ??= new EpisodeImages();
                    episode.Images.YouTube = video.First().GetImageUrl();
                    updated = true;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failure updating image for episode with id '{episodeId}' and youtube-video-id '{youtubeId}'.",
                    episode.Id,
                    episode.YouTubeId);
            }

        }
        if (updateRequest.UpdateBBCImage == true && episode.Urls.BBC != null)
        {
            try
            {
                var metaData = await bbcPageMetaDataExtractor.GetMetaData(episode.Urls.BBC);
                episode.Images ??= new EpisodeImages();
                episode.Images.Other = metaData.Image;
                updated = true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failure updating image for episode with id '{episodeId}' and iplayer-url '{iplayerUrl}'.",
                    episode.Id,
                    episode.Urls.BBC);
            }
        }
        return updated;
    }
}