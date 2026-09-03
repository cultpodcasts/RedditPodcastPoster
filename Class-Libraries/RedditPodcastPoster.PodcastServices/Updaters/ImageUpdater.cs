using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Apple.Factories;
using RedditPodcastPoster.PodcastServices.Apple.Resolvers;
using RedditPodcastPoster.PodcastServices.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Factories;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using RedditPodcastPoster.BBC.Extractors;
using RedditPodcastPoster.BBC.Matching;

namespace RedditPodcastPoster.PodcastServices.Updaters;

public class ImageUpdater(
    ISpotifyEpisodeResolver spotifyEpisodeResolver,
    IAppleEpisodeResolver appleEpisodeResolver,
    IYouTubeServiceWrapper youTubeService,
    IYouTubeVideoService youTubeVideoService,
    IYouTubeThumbnailResolver youTubeThumbnailResolver,
    IBBCPageMetaDataExtractor bbcPageMetaDataExtractor,
    ILogger<ImageUpdater> logger) : IImageUpdater
{
    public async Task<bool> UpdateImages(Podcast podcast, Episode episode, EpisodeImageUpdateRequest updateRequest,
        IndexingContext indexingContext)
    {
        var updated = false;
        if (updateRequest.UpdateSpotifyImage == true &&
            !string.IsNullOrWhiteSpace(EpisodeServicePresence.SpotifyEpisodeId(episode)))
        {
            try
            {
                var fullEpisode = await spotifyEpisodeResolver.FindEpisode(
                    FindSpotifyEpisodeRequestFactory.Create(EpisodeServicePresence.SpotifyEpisodeId(episode)!),
                    indexingContext);
                if (fullEpisode != null)
                {
                    EpisodeServicePresence.Upsert(
                        episode,
                        ServiceKeys.Spotify,
                        EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Spotify),
                        fullEpisode.FullEpisode?.GetBestImageUrl());
                    updated = true;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failure updating image for episode with id '{episodeId}' and spotify-id '{spotifyId}'.",
                    episode.Id,
                    EpisodeServicePresence.SpotifyEpisodeId(episode));
            }
        }

        if (updateRequest.UpdateAppleImage == true &&
            EpisodeServicePresence.AppleEpisodeId(episode) != null && podcast.AppleId != null)
        {
            try
            {
                var appleEpisode = await appleEpisodeResolver.FindEpisode(
                    FindAppleEpisodeRequestFactory.Create(podcast, episode),
                    indexingContext);
                if (appleEpisode != null)
                {
                    EpisodeServicePresence.Upsert(
                        episode,
                        ServiceKeys.Apple,
                        EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Apple),
                        appleEpisode.Image);
                    updated = true;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failure updating image for episode with id '{episodeId}' and apple-id '{appleId}'.",
                    episode.Id,
                    EpisodeServicePresence.AppleEpisodeId(episode));
            }
        }

        if (updateRequest.UpdateYouTubeImage == true &&
            !string.IsNullOrWhiteSpace(EpisodeServicePresence.YouTubeEpisodeId(episode)))
        {
            try
            {
                var youTubeId = EpisodeServicePresence.YouTubeEpisodeId(episode)!;
                var video = await youTubeVideoService.GetVideoContentDetails(youTubeService, [youTubeId],
                    indexingContext, true);
                if (video != null && video.Any())
                {
                    EpisodeServicePresence.Upsert(
                        episode,
                        ServiceKeys.YouTube,
                        EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.YouTube),
                        await youTubeThumbnailResolver.GetImageUrlAsync(video.First()));
                    updated = true;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failure updating image for episode with id '{episodeId}' and youtube-video-id '{youtubeId}'.",
                    episode.Id,
                    EpisodeServicePresence.YouTubeEpisodeId(episode));
            }
        }

        var bbcUrl = EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.BbcIplayer) ??
                     EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.BbcSounds);
        if (updateRequest.UpdateBBCImage == true && bbcUrl != null)
        {
            try
            {
                var metaData = await bbcPageMetaDataExtractor.GetMetaData(bbcUrl);
                var bbcKey = ServiceCatalog.TryResolveKey(bbcUrl) ?? ServiceKeys.BbcSounds;
                EpisodeServicePresence.Upsert(episode, bbcKey, bbcUrl, metaData.Image);
                updated = true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failure updating image for episode with id '{episodeId}' and iplayer-url '{iplayerUrl}'.",
                    episode.Id,
                    bbcUrl);
            }
        }

        return updated;
    }
}
