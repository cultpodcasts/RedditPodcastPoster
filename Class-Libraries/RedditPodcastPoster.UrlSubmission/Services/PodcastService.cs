using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Extensions;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Apple.Resolvers;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Models;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace RedditPodcastPoster.UrlSubmission.Services;

public class PodcastService(
    IYouTubeServiceWrapper youTubeService,
    IPodcastRepository podcastRepository,
    ISpotifyEpisodeResolver spotifyEpisodeResolver,
    IYouTubeVideoService youTubeVideoService,
    INonPodcastServiceAdapterResolver nonPodcastServiceAdapterResolver,
    IYouTubePlaylistMembership youTubePlaylistMembership,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<PodcastService> logger
#pragma warning restore CS9113 // Parameter is unread.
) : IPodcastService
{
    public async Task<Podcast?> GetPodcastFromEpisodeUrl(Uri url, IndexingContext indexingContext) =>
        (await GetEpisodeUrlOwner(url, indexingContext)).Podcast;

    public async Task<EpisodeUrlOwner> GetEpisodeUrlOwner(Uri url, IndexingContext indexingContext)
    {
        var ids = await GetOwnerIds(url, indexingContext);
        return new EpisodeUrlOwner(await MatchPodcast(ids), ids);
    }

    public async Task<bool?> IsVideoInPodcastPlaylist(Podcast podcast, Uri url, IndexingContext indexingContext)
    {
        if (string.IsNullOrWhiteSpace(podcast.YouTubePlaylistId))
        {
            return true;
        }

        if (string.Equals(YouTubePlaylistIdResolver.Extract(url), podcast.YouTubePlaylistId, StringComparison.Ordinal))
        {
            return true;
        }

        var videoId = YouTubeIdResolver.Extract(url);
        if (string.IsNullOrWhiteSpace(videoId) || indexingContext.SkipYouTubeUrlResolving)
        {
            return null;
        }

        return await youTubePlaylistMembership.Contains(youTubeService, podcast.YouTubePlaylistId, videoId);
    }

    private async Task<EpisodeUrlOwnerIds> GetOwnerIds(Uri url, IndexingContext indexingContext)
    {
        if (SpotifyPodcastServiceMatcher.IsMatch(url))
        {
            var episodeId = SpotifyIdResolver.GetEpisodeId(url);
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

            return new EpisodeUrlOwnerIds(SpotifyShowId: episode.FullEpisode.Show.Id);
        }

        if (YouTubePodcastServiceMatcher.IsMatch(url))
        {
            var videoId = YouTubeIdResolver.Extract(url);
            if (string.IsNullOrWhiteSpace(videoId))
            {
                throw new ArgumentException($"Unable to extract youtube-video-id from '{url}'.", nameof(url));
            }

            var episodes =
                await youTubeVideoService.GetVideoContentDetails(youTubeService, [videoId], indexingContext, true);
            if (episodes == null || !episodes.Any())
            {
                throw new InvalidOperationException($"Unable to find youtube-video for youtube-video-id '{videoId}'.");
            }

            var snippet = episodes.First().Snippet;
            return new EpisodeUrlOwnerIds(YouTubeChannelId: snippet.ChannelId, YouTubeChannelTitle: snippet.ChannelTitle);
        }

        if (ApplePodcastServiceMatcher.IsMatch(url))
        {
            var podcastId = AppleIdResolver.GetPodcastId(url);
            if (podcastId == null)
            {
                throw new ArgumentException($"Unable to extract apple-episode-id from '{url}'.", nameof(url));
            }

            return new EpisodeUrlOwnerIds(ApplePodcastId: podcastId);
        }

        if (nonPodcastServiceAdapterResolver.ForSubmit(url) != null)
        {
            return EpisodeUrlOwnerIds.None;
        }

        throw new ArgumentException($"Unable to determine service for url '{url}'.", nameof(url));
    }

    private async Task<Podcast?> MatchPodcast(EpisodeUrlOwnerIds ids)
    {
        IEnumerable<Podcast> v2Podcasts;
        if (ids.SpotifyShowId is { } spotifyShowId)
        {
            v2Podcasts = await podcastRepository.GetAllBy(podcast => podcast.SpotifyId == spotifyShowId)
                .ToArrayAsync();
        }
        else if (ids.YouTubeChannelId is { } snippetChannelId)
        {
            v2Podcasts = await podcastRepository.GetAllBy(podcast =>
                podcast.YouTubeChannelId == snippetChannelId).ToArrayAsync();
            if (v2Podcasts.Count() > 1)
            {
                v2Podcasts = v2Podcasts.Where(x => x.YouTubePlaylistId == string.Empty);
                if (v2Podcasts.Count() > 1 && v2Podcasts.Count(x =>
                        x.IndexAllEpisodes || !string.IsNullOrWhiteSpace(x.EpisodeIncludeTitleRegex)) == 1)
                {
                    v2Podcasts = v2Podcasts.Where(x =>
                        x.IndexAllEpisodes || !string.IsNullOrWhiteSpace(x.EpisodeIncludeTitleRegex));
                }

                if (v2Podcasts.Count() > 1)
                {
                    var videoChannelName = (ids.YouTubeChannelTitle ?? string.Empty).Trim().ToLowerInvariant();
                    v2Podcasts = v2Podcasts.Where(x => x.Name.Trim().ToLowerInvariant() == videoChannelName);
                    if (v2Podcasts.Count() != 1)
                    {
                        var message =
                            $"Multiple podcasts with youtube-channel-id '{snippetChannelId}'.";
                        var invalidOperationException = new InvalidOperationException(message);
                        logger.LogError(invalidOperationException, message);
                        throw invalidOperationException;
                    }
                }
            }
        }
        else if (ids.ApplePodcastId is { } podcastId)
        {
            v2Podcasts = await podcastRepository.GetAllBy(podcast => podcast.AppleId == podcastId).ToArrayAsync();
        }
        else
        {
            v2Podcasts = [];
        }

        if (v2Podcasts.Count() > 1)
        {
            v2Podcasts = v2Podcasts.Where(x => x.IndexAllEpisodes);
        }

        var v2Podcast = v2Podcasts.SingleOrDefault();
        return v2Podcast;
    }
}
