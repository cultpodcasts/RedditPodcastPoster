using System.Net;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Client;

public class SpotifyClientWrapper(ISpotifyClient spotifyClient, ILogger<SpotifyClientWrapper> logger)
    : ISpotifyClientWrapper
{
    public async Task<IList<T>?> Paginate<T>(
        IPaginatable<T> firstPage,
        IndexingContext indexingContext,
        IPaginator? paginator = null,
        CancellationToken cancel = default)
    {
        IList<T>? results = null;
        try
        {
            var items = spotifyClient.Paginate(firstPage, paginator, cancel);
            results = await items.ToListAsync(cancel);
        }
        catch (APITooManyRequestsException ex)
        {
            logger.LogError(ex,
                $"{nameof(Paginate)} Too-Many-Requests Failure with Spotify-API. Retry-after: '{ex.RetryAfter}'. Response: '{ex.Response?.Body ?? "<null>"}'.");
            indexingContext.SkipSpotifyUrlResolving = true;
            return null;
        }
        catch (APIException ex)
        {
            logger.LogError(ex,
                $"{nameof(Paginate)} Failure with Spotify-API. Response: '{ex.Response?.Body ?? "<null>"}'.");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(Paginate)} Failure with Spotify-API.");
            return null;
        }

        return results;
    }

    public async Task<IList<T>?> PaginateAll<T>(
        IPaginatable<T> firstPage,
        IndexingContext indexingContext,
        IPaginator? paginator = null)
    {
        IList<T>? results;
        try
        {
            results = await spotifyClient.PaginateAll(firstPage);
        }
        catch (APITooManyRequestsException ex)
        {
            logger.LogError(ex,
                $"{nameof(PaginateAll)} Too-Many-Requests Failure with Spotify-API. Retry-after: '{ex.RetryAfter}'. Response: '{ex.Response?.Body ?? "<null>"}'.");
            indexingContext.SkipSpotifyUrlResolving = true;
            return null;
        }
        catch (APIException ex)
        {
            logger.LogError(ex,
                $"{nameof(PaginateAll)} Failure with Spotify-API. Response: '{ex.Response?.Body ?? "<null>"}'.");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(PaginateAll)} Failure with Spotify-API.");
            return null;
        }

        return results;
    }

    public async Task<IList<T>?> PaginateAll<T, T1>(
        IPaginatable<T, T1> firstPage,
        Func<T1, IPaginatable<T, T1>> mapper,
        IndexingContext indexingContext,
        IPaginator? paginator = null)
    {
        var results = firstPage.Items ?? new List<T>();
        try
        {
            var batch = await spotifyClient.PaginateAll(firstPage, mapper);
            results.AddRange(batch);
        }
        catch (APITooManyRequestsException ex)
        {
            logger.LogError(ex,
                $"{nameof(PaginateAll)} Too-Many-Requests Failure with Spotify-API. Retry-after: '{ex.RetryAfter}'. Response: '{ex.Response?.Body ?? "<null>"}'.");
            indexingContext.SkipSpotifyUrlResolving = true;
            return null;
        }
        catch (APIException ex)
        {
            logger.LogError(ex,
                $"{nameof(PaginateAll)} Failure with Spotify-API. Response: '{ex.Response?.Body ?? "<null>"}'.");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(PaginateAll)} Failure with Spotify-API.");
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
            results = await spotifyClient.Shows.GetEpisodes(showId, request, cancel);
        }
        catch (APITooManyRequestsException ex)
        {
            logger.LogError(ex,
                "{nameofGetShowEpisodes} Too-Many-Requests Failure with Spotify-API. Retry-after: '{exRetryAfter}'. Response: '{body}'.",
                nameof(GetShowEpisodes), ex.RetryAfter, ex.Response?.Body ?? "<null>");
            indexingContext.SkipSpotifyUrlResolving = true;
            return null;
        }
        catch (JsonSerializationException ex)
        {
            logger.LogError(ex,
                "{nameofGetShowEpisodes} Failure deserializing response from Spotify-API for show '{showId}'.",
                nameof(GetShowEpisodes), showId);
            return null;
        }
        catch (APIException ex)
        {
            logger.LogError(ex,
                "{nameofGetShowEpisodes} Failure with Spotify-API for show '{showId}'. Response: '{body}'.",
                nameof(GetShowEpisodes), showId, ex.Response?.Body ?? "<null>");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{nameofGetShowEpisodes} Failure with Spotify-API. Show-id: '{showId}'.",
                nameof(GetShowEpisodes), showId);
            indexingContext.SkipSpotifyUrlResolving = true;
            return null;
        }

        return results;
    }

    public async Task<List<SimpleShow>> GetSimpleShows(SearchRequest request, IndexingContext indexingContext,
        CancellationToken cancel = default)
    {
        var searchResponse = await GetSearchResponse(request, indexingContext, cancel);
        if (searchResponse?.Shows.Items == null)
        {
            return [];
        }

#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
        List<SimpleShow?> items = searchResponse.Shows.Items;
        return items.Where(x => x != null).ToList();
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.
    }

    public async Task<SearchResponse?> GetSearchResponse(
        SearchRequest request,
        IndexingContext indexingContext,
        CancellationToken cancel = default)
    {
        SearchResponse? results = null;
        try
        {
            results = await spotifyClient.Search.Item(request, cancel);
        }
        catch (APITooManyRequestsException ex)
        {
            logger.LogError(ex,
                $"{nameof(GetSearchResponse)} Too-Many-Requests Failure with Spotify-API. Retry-after: '{ex.RetryAfter}'. Response: '{ex.Response?.Body ?? "<null>"}'.");
            indexingContext.SkipSpotifyUrlResolving = true;
            return null;
        }
        catch (APIException ex)
        {
            logger.LogError(ex,
                $"{nameof(GetSearchResponse)} Failure with Spotify-API. Response: '{ex.Response?.Body ?? "<null>"}'.");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                $"{nameof(GetSearchResponse)} Failure with Spotify-API. Search-query: '{request.Query}'.");
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
            results = await spotifyClient.Shows.Get(showId, request, cancel);
        }
        catch (APITooManyRequestsException ex)
        {
            logger.LogError(ex,
                $"{nameof(GetFullShow)} Too-Many-Requests Failure with Spotify-API. Retry-after: '{ex.RetryAfter}'. Response: '{ex.Response?.Body ?? "<null>"}'.");
            indexingContext.SkipSpotifyUrlResolving = true;
            return null;
        }
        catch (APIException ex)
        {
            logger.LogError(ex,
                $"{nameof(GetFullShow)} Failure with Spotify-API. Response: '{ex.Response?.Body ?? "<null>"}'.");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(GetFullShow)} Failure with Spotify-API. Show-id: '{showId}'.");
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
            results = await spotifyClient.Episodes.Get(episodeId, request, cancel);
        }
        catch (APITooManyRequestsException ex)
        {
            logger.LogError(ex,
                "{method} Too-Many-Requests Failure with Spotify-API. Retry-after: '{retryAfter}'. Response: '{responseBody}'.",
                nameof(GetFullEpisode), ex.RetryAfter, ex.Response?.Body ?? "<null>");
            indexingContext.SkipSpotifyUrlResolving = true;
            return null;
        }
        catch (APIException ex)
        {
            if (ex.Message.StartsWith("Non existing id:") || ex.Response?.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogError(ex,
                    "{method} Failure with Spotify-API. Item with '{episodeId}' not found. Response: '{responseBody}'.",
                    nameof(GetFullEpisode), episodeId, ex.Response?.Body ?? "<null>");
                throw new EpisodeNotFoundException(episodeId, Service.Spotify);
            }

            logger.LogError(ex,
                "{method} Failure with Spotify-API. Response: '{responseBody}'.",
                nameof(GetFullEpisode), ex.Response?.Body ?? "<null>");

            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method} Failure with Spotify-API. Episode-id: '{episodeId}'.",
                nameof(GetFullEpisode), episodeId);
            return null;
        }

        return results;
    }

    public async Task<EpisodesResponse?> GetSeveral(
        EpisodesRequest request,
        IndexingContext indexingContext,
        CancellationToken cancel = default)
    {
        EpisodesResponse? results = null;
        try
        {
            results = await spotifyClient.Episodes.GetSeveral(request, cancel);
        }
        catch (APITooManyRequestsException ex)
        {
            logger.LogError(ex,
                $"{nameof(GetSeveral)} Too-Many-Requests Failure with Spotify-API. Retry-after: '{ex.RetryAfter}'. Response: '{ex.Response?.Body ?? "<null>"}'.");
            indexingContext.SkipSpotifyUrlResolving = true;
            return null;
        }
        catch (APIException ex)
        {
            logger.LogError(ex,
                $"{nameof(GetSeveral)} Failure with Spotify-API. Response: '{ex.Response?.Body ?? "<null>"}'.");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(GetSeveral)} Failure with Spotify-API.");
            return null;
        }

        return results;
    }

    public async Task<Paging<SimpleEpisode, SearchResponse>?> FindEpisodes(
        SearchRequest request,
        IndexingContext indexingContext,
        CancellationToken cancel = default)
    {
        Paging<SimpleEpisode, SearchResponse> results;
        try
        {
            var response = await spotifyClient.Search.Item(request, cancel);
            results = response.Episodes;
        }
        catch (APITooManyRequestsException ex)
        {
            logger.LogError(ex,
                $"{nameof(FindEpisodes)} Too-Many-Requests Failure with Spotify-API. Retry-after: '{ex.RetryAfter}'. Response: '{ex.Response?.Body ?? "<null>"}'.");
            indexingContext.SkipSpotifyUrlResolving = true;
            return null;
        }
        catch (APIException ex)
        {
            logger.LogError(ex,
                $"{nameof(FindEpisodes)} Failure with Spotify-API. Response: '{ex.Response?.Body ?? "<null>"}'.");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(FindEpisodes)} Failure with Spotify-API. Search-query '{request.Query}'.");
            return null;
        }

        return results;
    }
}