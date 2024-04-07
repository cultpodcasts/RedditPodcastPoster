using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.YouTube;
using Podcast = RedditPodcastPoster.Models.Podcast;

namespace RedditPodcastPoster.UrlSubmission;

public class PodcastService(
    IPodcastRepository podcastRepository,
    ISpotifyUrlCategoriser spotifyUrlCategoriser,
    ISpotifyEpisodeResolver spotifyEpisodeResolver,
    IAppleUrlCategoriser appleUrlCategoriser,
    IYouTubeUrlCategoriser youTubeUrlCategoriser,
    IYouTubeIdExtractor youTubeIdExtractor,
    IYouTubeVideoService youTubeVideoService,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<PodcastService> logger
#pragma warning restore CS9113 // Parameter is unread.
) : IPodcastService
{
    public async Task<Podcast?> GetPodcastFromEpisodeUrl(Uri url, IndexingContext indexingContext)
    {
        IEnumerable<Podcast> podcasts;
        if (spotifyUrlCategoriser.IsMatch(url))
        {
            var episodeId = spotifyUrlCategoriser.GetEpisodeId(url);
            if (string.IsNullOrWhiteSpace(episodeId))
            {
                throw new ArgumentException($"Unable to extract spotify-episode-id from '{url}'.", nameof(url));
            }

            var findSpotifyEpisodeRequest = new FindSpotifyEpisodeRequest(string.Empty, string.Empty,
                episodeId, string.Empty, null, false, Market: Market.CountryCode);
            var episode = await spotifyEpisodeResolver.FindEpisode(findSpotifyEpisodeRequest, indexingContext);
            if (episode.FullEpisode == null)
            {
                throw new InvalidOperationException(
                    $"Unable to find spotify-full-show for spotify-episode with spotify-episode-id '{episodeId}'.");
            }

            podcasts = await podcastRepository.GetAllBy(podcast => podcast.SpotifyId == episode.FullEpisode.Show.Id)
                .ToArrayAsync();
        }
        else if (youTubeUrlCategoriser.IsMatch(url))
        {
            var videoId = youTubeIdExtractor.Extract(url);
            if (string.IsNullOrWhiteSpace(videoId))
            {
                throw new ArgumentException($"Unable to extract youtube-video-id from '{url}'.", nameof(url));
            }

            var episodes = await youTubeVideoService.GetVideoContentDetails(new[] {videoId}, indexingContext, true);
            if (episodes == null || !episodes.Any())
            {
                throw new InvalidOperationException($"Unable to find youtube-video for youtube-video-id '{videoId}'.");
            }

            var snippetChannelId = episodes.FirstOrDefault()!.Snippet.ChannelId;


            podcasts = await podcastRepository.GetAllBy(podcast =>
                podcast.YouTubeChannelId == snippetChannelId).ToArrayAsync();
            if (podcasts.Count() > 1)
            {
                podcasts = podcasts.Where(x => x.YouTubePlaylistId == string.Empty);
                if (podcasts.Count() > 1)
                {
                    throw new InvalidOperationException(
                        $"Multiple podcasts with youtube-channel-id '{snippetChannelId}' and empty youtube-playlist-id found");
                }
            }
        }
        else if (appleUrlCategoriser.IsMatch(url))
        {
            var podcastId = appleUrlCategoriser.GetPodcastId(url);
            if (podcastId == null)
            {
                throw new ArgumentException($"Unable to extract apple-episode-id from '{url}'.", nameof(url));
            }


            podcasts = await podcastRepository.GetAllBy(podcast => podcast.AppleId == podcastId).ToArrayAsync();
        }
        else
        {
            throw new ArgumentException($"Unable to determine service for url '{url}'.", nameof(url));
        }

        if (podcasts.Count() > 1)
        {
            podcasts = podcasts.Where(x => x.IndexAllEpisodes);
        }

        return podcasts.SingleOrDefault();
    }
}