using Microsoft.Extensions.Logging;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

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
            var fetch = await _spotifyClientWrapper.PaginateAll(pagedEpisodes, indexingContext);
            if (fetch != null)
            {
                episodes = fetch.ToList();
            }
        }
        else
        {
            while (episodes.OrderByDescending(x => x.ReleaseDate).Last().GetReleaseDate() >=
                   indexingContext.ReleasedSince)
            {
                var batchEpisodes = await _spotifyClientWrapper.Paginate(pagedEpisodes, indexingContext);
                if (batchEpisodes != null)
                {
                    episodes.AddRange(batchEpisodes);
                }
            }
        }

        return new PaginateEpisodesResponse(episodes, isExpensiveQueryFound);
    }
}