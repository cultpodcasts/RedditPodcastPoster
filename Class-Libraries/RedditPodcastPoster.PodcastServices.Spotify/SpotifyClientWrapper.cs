﻿using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public class SpotifyClientWrapper : ISpotifyClientWrapper
{
    private readonly ILogger<SpotifyClientWrapper> _logger;
    private readonly ISpotifyClient _spotifyClient;

    public SpotifyClientWrapper(ISpotifyClient spotifyClient, ILogger<SpotifyClientWrapper> logger)
    {
        _spotifyClient = spotifyClient;
        _logger = logger;
    }

    public async Task<IList<T>?> Paginate<T>(
        IPaginatable<T> firstPage,
        IndexingContext indexingContext,
        IPaginator? paginator = null,
        CancellationToken cancel = default)
    {
        IList<T>? results = null;
        try
        {
            results = await _spotifyClient.Paginate(firstPage, paginator, cancel).ToListAsync(cancel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"{nameof(Paginate)} Failure with Spotify-API.");
            indexingContext.SkipSpotifyUrlResolving = true;
            return null;
        }

        return results;
    }

    public async Task<IList<T>?> PaginateAll<T>(
        IPaginatable<T> firstPage,
        IndexingContext indexingContext,
        IPaginator? paginator = null)
    {
        IList<T>? results = null;
        try
        {
            if (firstPage.Next != null && firstPage.Next.Contains("/show/"))
            {
                firstPage.Next = firstPage.Next.Replace("/show/", "/shows/");
            }

            results = await _spotifyClient.PaginateAll(firstPage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"{nameof(PaginateAll)} Failure with Spotify-API.");
            indexingContext.SkipSpotifyUrlResolving = true;
            return null;
        }

        return results;
    }

    public async Task<Paging<SimpleEpisode>?> GetShowEpisodes(
        string showId,
        ShowEpisodesRequest request,
        IndexingContext indexingContext,
        CancellationToken cancel = default)
    {
        Paging<SimpleEpisode>? results = null;
        try
        {
            results = await _spotifyClient.Shows.GetEpisodes(showId, request, cancel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"{nameof(GetShowEpisodes)} Failure with Spotify-API.");
            indexingContext.SkipSpotifyUrlResolving = true;
            return null;
        }

        return results;
    }

    public async Task<SearchResponse?> GetSearchResponse(
        SearchRequest request,
        IndexingContext indexingContext,
        CancellationToken cancel = default)
    {
        SearchResponse? results = null;
        try
        {
            results = await _spotifyClient.Search.Item(request, cancel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"{nameof(GetSearchResponse)} Failure with Spotify-API.");
            indexingContext.SkipSpotifyUrlResolving = true;
            return null;
        }

        return results;
    }

    public async Task<FullShow?> GetFullShow(
        string showId,
        ShowRequest request,
        IndexingContext indexingContext,
        CancellationToken cancel = default)
    {
        FullShow? results = null;
        try
        {
            results = await _spotifyClient.Shows.Get(showId, request, cancel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"{nameof(GetFullEpisode)} Failure with Spotify-API.");
            indexingContext.SkipSpotifyUrlResolving = true;
            return null;
        }

        return results;
    }

    public async Task<FullEpisode?> GetFullEpisode(
        string episodeId,
        EpisodeRequest request,
        IndexingContext indexingContext,
        CancellationToken cancel = default)
    {
        FullEpisode? results = null;
        try
        {
            results = await _spotifyClient.Episodes.Get(episodeId, request, cancel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"{nameof(GetFullEpisode)} Failure with Spotify-API.");
            indexingContext.SkipSpotifyUrlResolving = true;
            return null;
        }

        return results;
    }
}