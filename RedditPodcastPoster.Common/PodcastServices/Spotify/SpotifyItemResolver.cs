﻿using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Text;
using SpotifyAPI.Web;
using System.Text.RegularExpressions;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public class SpotifyItemResolver : ISpotifyItemResolver
{
    public const string Market = "GB";
    private readonly ILogger<SpotifyItemResolver> _logger;
    private readonly ISpotifyClient _spotifyClient;
    private readonly ISpotifySearcher _spotifySearcher;

    public SpotifyItemResolver(
        ISpotifyClient spotifyClient,
        ISpotifySearcher spotifySearcher,
        ILogger<SpotifyItemResolver> logger)
    {
        _spotifyClient = spotifyClient;
        _spotifySearcher = spotifySearcher;
        _logger = logger;
    }

    public async Task<SpotifyEpisodeWrapper> FindEpisode(FindSpotifyEpisodeRequest request)
    {
        FullEpisode? fullEpisode = null;
        if (!string.IsNullOrWhiteSpace(request.EpisodeSpotifyId))
        {
            fullEpisode = await _spotifyClient.Episodes.Get(request.EpisodeSpotifyId);
        }

        if (fullEpisode == null)
        {
            Paging<SimpleEpisode>[] episodes;
            if (!string.IsNullOrWhiteSpace(request.PodcastSpotifyId))
            {
                var fullShow = await _spotifyClient.Shows.Get(request.PodcastSpotifyId,
                    new ShowRequest {Market = Market});
                episodes = new[] {fullShow.Episodes};
            }
            else
            {
                var podcastSearchResponse = await _spotifyClient.Search.Item(
                    new SearchRequest(SearchRequest.Types.Show, request.PodcastName)
                        {Market = Market});

                var podcasts = podcastSearchResponse.Shows.Items;
                var matchingPodcasts = _spotifySearcher.FindMatchingPodcasts(request.PodcastName, podcasts);
                var episodesFetches = matchingPodcasts.Select(async x =>
                    await _spotifyClient.Shows.GetEpisodes(x.Id,
                        new ShowEpisodesRequest {Market = Market}));
                episodes = await Task.WhenAll(episodesFetches);
            }

            IList<IList<SimpleEpisode>> allEpisodes = new List<IList<SimpleEpisode>>();
            foreach (var paging in episodes)
            {
                var simpleEpisodes = await _spotifyClient.PaginateAll(paging);
                allEpisodes.Add(simpleEpisodes);
            }

            var matchingEpisode =
                _spotifySearcher.FindMatchingEpisode(request.EpisodeTitle, request.Released, allEpisodes);
            if (matchingEpisode != null)
            {
                fullEpisode = await _spotifyClient.Episodes.Get(matchingEpisode.Id,
                    new EpisodeRequest {Market = Market});
            }
        }

        return new SpotifyEpisodeWrapper(fullEpisode);
    }

    public async Task<SpotifyPodcastWrapper> FindPodcast(FindSpotifyPodcastRequest request)
    {
        SimpleShow? matchingSimpleShow = null;
        FullShow? matchingFullShow = null;
        if (!string.IsNullOrWhiteSpace(request.SpotifyId))
        {
            matchingFullShow = await _spotifyClient.Shows.Get(request.SpotifyId);
        }

        if (matchingFullShow == null)
        {
            var podcasts = await _spotifyClient.Search.Item(new SearchRequest(SearchRequest.Types.Show, request.Name)
                {Market = Market});

            var matchingPodcasts = _spotifySearcher.FindMatchingPodcasts(request.Name, podcasts.Shows.Items);
            if (request.Episodes.Any())
            {
                foreach (var candidatePodcast in matchingPodcasts)
                {
                    var pagedEpisodes = await _spotifyClient.Shows.GetEpisodes(candidatePodcast.Id,
                        new ShowEpisodesRequest {Market = Market});

                    var allEpisodes = await _spotifyClient.PaginateAll(pagedEpisodes);

                    var mostRecentEpisode = request.Episodes.OrderByDescending(x => x.Release).First();
                    var matchingEpisode =
                        _spotifySearcher.FindMatchingEpisode(
                            mostRecentEpisode.Title,
                            mostRecentEpisode.Release,
                            new[] {allEpisodes});
                    if (request.Episodes
                        .Select(x => x.Url?.ToString())
                        .Contains(matchingEpisode!.ExternalUrls.FirstOrDefault().Value))
                    {
                        matchingSimpleShow = candidatePodcast;
                        break;
                    }
                }
            }

            matchingSimpleShow = matchingPodcasts.MaxBy(x => Levenshtein.CalculateSimilarity(request.Name, x.Name));
        }

        return new SpotifyPodcastWrapper(matchingFullShow, matchingSimpleShow);
    }

    public async Task<IEnumerable<SimpleEpisode>> GetEpisodes(string spotifyId, DateTime? releasedSince)
    {
        var episodes =
            await _spotifyClient.Shows.GetEpisodes(spotifyId,
                new ShowEpisodesRequest {Market = Market});
        List<SimpleEpisode> allEpisodes = new List<SimpleEpisode>();
        try
        {
            if (releasedSince == null)
            {
                var fetch = await _spotifyClient.PaginateAll(episodes);
                allEpisodes= fetch.ToList();
            }
            else
            {
                while (episodes.Items.Last().GetReleaseDate() > releasedSince)
                {
                    await _spotifyClient.Paginate(episodes).ToListAsync();
                }

                allEpisodes = episodes.Items;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failure to retrieve episodes from Spotify with spotify-id '{spotifyId}'.");
            return Enumerable.Empty<SimpleEpisode>();
        }

        return allEpisodes;
    }
}