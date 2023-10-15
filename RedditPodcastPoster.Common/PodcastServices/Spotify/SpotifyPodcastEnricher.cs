﻿using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public class SpotifyPodcastEnricher : ISpotifyPodcastEnricher
{
    private readonly ILogger<SpotifyPodcastEnricher> _logger;
    private readonly ISpotifyEpisodeResolver _spotifyEpisodeResolver;
    private readonly ISpotifyPodcastResolver _spotifyPodcastResolver;

    public SpotifyPodcastEnricher(
        ISpotifyEpisodeResolver spotifyIdResolver,
        ISpotifyPodcastResolver spotifyPodcastResolver,
        ILogger<SpotifyPodcastEnricher> logger)
    {
        _spotifyEpisodeResolver = spotifyIdResolver;
        _spotifyPodcastResolver = spotifyPodcastResolver;
        _logger = logger;
    }

    public async Task<bool> AddIdAndUrls(Podcast podcast, IndexingContext indexingContext)
    {
        var podcastShouldUpdate = false;
        if (string.IsNullOrWhiteSpace(podcast.SpotifyId))
        {
            var matchedPodcast =
                await _spotifyPodcastResolver.FindPodcast(podcast.ToFindSpotifyPodcastRequest(), indexingContext);
            if (matchedPodcast != null && !string.IsNullOrWhiteSpace(matchedPodcast.Id))
            {
                podcast.SpotifyId = matchedPodcast.Id;
                podcastShouldUpdate = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(podcast.SpotifyId))
        {
            foreach (var podcastEpisode in podcast.Episodes)
            {
                if (string.IsNullOrWhiteSpace(podcastEpisode.SpotifyId))
                {
                    var episode =
                        await _spotifyEpisodeResolver.FindEpisode(
                            FindSpotifyEpisodeRequestFactory.Create(podcast, podcastEpisode), indexingContext);
                    if (!string.IsNullOrWhiteSpace(episode?.Id))
                    {
                        podcastEpisode.SpotifyId = episode.Id;
                        podcastShouldUpdate = true;
                    }
                }
            }
        }

        return podcastShouldUpdate;
    }
}