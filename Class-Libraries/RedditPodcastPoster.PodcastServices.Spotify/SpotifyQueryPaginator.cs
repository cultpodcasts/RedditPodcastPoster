using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public class SpotifyQueryPaginator(
    ISpotifyClientWrapper spotifyClientWrapper,
    ILogger<SpotifyQueryPaginator> logger)
    : ISpotifyQueryPaginator
{
    public async Task<PaginateEpisodesResponse> PaginateEpisodes(
        IPaginatable<SimpleEpisode>? pagedEpisodes,
        IndexingContext indexingContext)
    {
        if (indexingContext.SkipSpotifyUrlResolving)
        {
            logger.LogInformation(
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
        var existingPagedEpisodes = pagedEpisodes.Items.Where(x => x != null).ToArray();
        while (isInReverseTimeOrder && ctr < existingPagedEpisodes.Length)
        {
            var releaseDate = existingPagedEpisodes[ctr].GetReleaseDate();
            isInReverseTimeOrder = currentMoment >= releaseDate;
            if (isInReverseTimeOrder)
            {
                currentMoment = releaseDate;
            }

            ctr++;
        }

        var isExpensiveQueryFound = !isInReverseTimeOrder;

        var episodes = pagedEpisodes.Items.ToList();

        if (indexingContext.ReleasedSince == null || isExpensiveQueryFound)
        {
            if (pagedEpisodes.Next != null && pagedEpisodes.Next.Contains("/show/"))
            {
                pagedEpisodes.Next = pagedEpisodes.Next.Replace("/show/", "/shows/");
            }

            var fetch = await spotifyClientWrapper.PaginateAll(pagedEpisodes, indexingContext);
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
                   episodes.Where(x => x != null).OrderByDescending(x => x.ReleaseDate).Last().GetReleaseDate() >=
                   indexingContext.ReleasedSince)
            {
                var preCount = batchEpisodes.Count;
                var items = await spotifyClientWrapper.Paginate(pagedEpisodes, indexingContext);
                if (items != null)
                {
                    batchEpisodes = items;
                }

                seenGrowth = items != null && batchEpisodes.Count > preCount;
            }

            episodes.AddRange(batchEpisodes);
        }

        return new PaginateEpisodesResponse(episodes.Where(x => x != null).ToList(), isExpensiveQueryFound);
    }
}