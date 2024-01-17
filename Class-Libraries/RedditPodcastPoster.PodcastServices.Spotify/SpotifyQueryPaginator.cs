using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public class SpotifyQueryPaginator : ISpotifyQueryPaginator
{
    private readonly ILogger<SpotifyQueryPaginator> _logger;
    private readonly ISpotifyClientWrapper _spotifyClientWrapper;

    public SpotifyQueryPaginator(
        ISpotifyClientWrapper spotifyClientWrapper,
        ILogger<SpotifyQueryPaginator> logger)
    {
        _spotifyClientWrapper = spotifyClientWrapper;
        _logger = logger;
    }

    public async Task<PaginateEpisodesResponse> PaginateEpisodes(
        IPaginatable<SimpleEpisode>? pagedEpisodes,
        IndexingContext indexingContext)
    {
        if (indexingContext.SkipSpotifyUrlResolving)
        {
            _logger.LogInformation(
                $"Skipping '{nameof(PaginateEpisodes)}' as '{nameof(indexingContext.SkipSpotifyUrlResolving)}' is set.");
            return new PaginateEpisodesResponse(new List<SimpleEpisode>());
        }

        if (pagedEpisodes == null || pagedEpisodes.Items == null)
        {
            return new PaginateEpisodesResponse(new List<SimpleEpisode>());
        }

        var currentMoment = DateTime.Now;
        var isInReverseTimeOrder = true;
        var ctr = 0;
        while (isInReverseTimeOrder && ctr < pagedEpisodes.Items.Count)
        {
            var releaseDate = pagedEpisodes.Items[ctr++].GetReleaseDate();
            isInReverseTimeOrder = currentMoment >= releaseDate;
            if (isInReverseTimeOrder)
            {
                currentMoment = releaseDate;
            }
        }

        var isExpensiveQueryFound = !isInReverseTimeOrder;

        var episodes = pagedEpisodes.Items.ToList();

        if (indexingContext.ReleasedSince == null || isExpensiveQueryFound)
        {
            if (pagedEpisodes.Next != null && pagedEpisodes.Next.Contains("/show/"))
            {
                pagedEpisodes.Next = pagedEpisodes.Next.Replace("/show/", "/shows/");
            }

            var fetch = await _spotifyClientWrapper.PaginateAll(pagedEpisodes, indexingContext);
            if (fetch != null)
            {
                episodes = fetch.ToList();
            }
        }
        else
        {
            IList<SimpleEpisode>? batchEpisodes = new List<SimpleEpisode>();
            var seenGrowth = true;
            while (seenGrowth &&
                   episodes.OrderByDescending(x => x.ReleaseDate).Last().GetReleaseDate() >=
                   indexingContext.ReleasedSince)
            {
                var preCount = batchEpisodes.Count;
                var items = await _spotifyClientWrapper.Paginate(pagedEpisodes, indexingContext);
                if (items != null)
                {
                    batchEpisodes = items;
                }
                seenGrowth = items != null && batchEpisodes.Count > preCount;
            }

            episodes.AddRange(batchEpisodes);
        }

        return new PaginateEpisodesResponse(episodes, isExpensiveQueryFound);
    }
}