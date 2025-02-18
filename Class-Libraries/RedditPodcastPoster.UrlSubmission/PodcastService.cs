﻿using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using Podcast = RedditPodcastPoster.Models.Podcast;

namespace RedditPodcastPoster.UrlSubmission;

public class PodcastService(
    IYouTubeServiceWrapper youTubeService,
    IPodcastRepository podcastRepository,
    ISpotifyEpisodeResolver spotifyEpisodeResolver,
    IYouTubeVideoService youTubeVideoService,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<PodcastService> logger
#pragma warning restore CS9113 // Parameter is unread.
) : IPodcastService
{
    public async Task<Podcast?> GetPodcastFromEpisodeUrl(Uri url, IndexingContext indexingContext)
    {
        IEnumerable<Podcast> podcasts;
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

            podcasts = await podcastRepository.GetAllBy(podcast => podcast.SpotifyId == episode.FullEpisode.Show.Id)
                .ToArrayAsync();
        }
        else if (YouTubePodcastServiceMatcher.IsMatch(url))
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

            var snippetChannelId = episodes.FirstOrDefault()!.Snippet.ChannelId;

            podcasts = await podcastRepository.GetAllBy(podcast =>
                podcast.YouTubeChannelId == snippetChannelId).ToArrayAsync();
            if (podcasts.Count() > 1)
            {
                podcasts = podcasts.Where(x => x.YouTubePlaylistId == string.Empty);
                if (podcasts.Count() > 1 && podcasts.Count(x =>
                        x.IndexAllEpisodes || !string.IsNullOrWhiteSpace(x.EpisodeIncludeTitleRegex)) == 1)
                {
                    podcasts = podcasts.Where(x =>
                        x.IndexAllEpisodes || !string.IsNullOrWhiteSpace(x.EpisodeIncludeTitleRegex));
                }

                if (podcasts.Count() > 1)
                {
                    var videoChannelName = episodes.First().Snippet.ChannelTitle.Trim().ToLowerInvariant();
                    podcasts = podcasts.Where(x => x.Name.Trim().ToLowerInvariant() == videoChannelName);
                    if (podcasts.Count() != 1)
                    {
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
        }
        else if (ApplePodcastServiceMatcher.IsMatch(url))
        {
            var podcastId = AppleIdResolver.GetPodcastId(url);
            if (podcastId == null)
            {
                throw new ArgumentException($"Unable to extract apple-episode-id from '{url}'.", nameof(url));
            }

            podcasts = await podcastRepository.GetAllBy(podcast => podcast.AppleId == podcastId).ToArrayAsync();
        }
        else if (NonPodcastServiceMatcher.MatchesBBC(url) || NonPodcastServiceMatcher.MatchesInternetArchive(url))
        {
            podcasts = [];
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