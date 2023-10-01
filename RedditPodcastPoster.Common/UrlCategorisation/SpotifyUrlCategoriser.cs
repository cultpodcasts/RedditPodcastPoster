﻿using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.PodcastServices.Spotify;
using RedditPodcastPoster.Models;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.Common.UrlCategorisation;

public class SpotifyUrlCategoriser : ISpotifyUrlCategoriser
{
    private static readonly Regex SpotifyId = new(@"episode/(?'episodeId'\w+)");
    private readonly ILogger<SpotifyUrlCategoriser> _logger;
    private readonly ISpotifyClient _spotifyClient;
    private readonly ISpotifyItemResolver _spotifyItemResolver;

    public SpotifyUrlCategoriser(
        ISpotifyClient spotifyClient,
        ISpotifyItemResolver spotifyItemResolver,
        ILogger<SpotifyUrlCategoriser> logger)
    {
        _spotifyClient = spotifyClient;
        _spotifyItemResolver = spotifyItemResolver;
        _logger = logger;
    }

    public bool IsMatch(Uri url)
    {
        return url.Host.ToLower().Contains("spotify");
    }

    public async Task<ResolvedSpotifyItem> Resolve(List<Podcast> podcasts, Uri url)
    {
        var pair = podcasts
            .SelectMany(podcast => podcast.Episodes, (podcast, episode) => new PodcastEpisodePair(podcast, episode))
            .FirstOrDefault(pair => pair.Episode.Urls.Spotify == url);

        if (pair != null)
        {
            return new ResolvedSpotifyItem(pair);
        }

        var episodeId = SpotifyId.Match(url.ToString()).Groups["episodeId"].Value;
        if (episodeId == null)
        {
            throw new InvalidOperationException($"Unable to find spotify-id in url '{url}'.");
        }

        var item = await _spotifyClient.Episodes.Get(episodeId,
            new EpisodeRequest {Market = SpotifyItemResolver.Market});
        if (item != null)
        {
            return new ResolvedSpotifyItem(
                item.Show.Id,
                item.Id,
                item.Show.Name,
                item.Show.Description,
                item.Show.Publisher,
                item.Name,
                item.Description,
                item.GetReleaseDate(),
                item.GetDuration(),
                new Uri(item.ExternalUrls.FirstOrDefault().Value, UriKind.Absolute),
                item.Explicit);
        }

        throw new InvalidOperationException($"Could not find item with spotify-id '{SpotifyId}'.");
    }

    public async Task<ResolvedSpotifyItem?> Resolve(PodcastServiceSearchCriteria criteria, Podcast? matchingPodcast, IndexOptions indexOptions)
    {
        var request = new FindSpotifyEpisodeRequest(
            matchingPodcast?.SpotifyId ?? string.Empty,
            (matchingPodcast?.Name ?? criteria.ShowName).Trim(),
            string.Empty,
            criteria.EpisodeTitle.Trim(),
            criteria.Release);
        var item = await _spotifyItemResolver.FindEpisode(request, indexOptions);
        if (item != null)
        {
            return new ResolvedSpotifyItem(
                item.Show.Id,
                item.Id,
                item.Show.Name,
                item.Show.Description,
                item.Show.Publisher,
                item.Name,
                item.Description,
                item.GetReleaseDate(),
                item.GetDuration(),
                item.GetUrl(),
                item.Explicit);
        }

        _logger.LogWarning(
            $"Could not find spotify episode for show named '{criteria.ShowName}' and episode-name '{criteria.EpisodeTitle}'.");
        return null;
    }
}