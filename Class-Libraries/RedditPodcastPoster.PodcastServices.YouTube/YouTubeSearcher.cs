using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public class YouTubeSearcher(
    YouTubeService youTubeService,
    ILogger<YouTubeSearcher> logger) : IYouTubeSearcher
{
    private const long MaxSearchResults = 25;

    public async Task<IEnumerable<EpisodeResult>> Search(string query, IndexingContext indexingContext)
    {
        var result = new List<SearchResult>();
        var nextPageToken = "";
        var searchListRequest = youTubeService.Search.List("snippet");
        searchListRequest.MaxResults = MaxSearchResults;
        searchListRequest.PageToken = nextPageToken; // or searchListResponse.NextPageToken if paging
        searchListRequest.Type = "video";
        searchListRequest.SafeSearch = SearchResource.ListRequest.SafeSearchEnum.None;
        searchListRequest.Order = SearchResource.ListRequest.OrderEnum.Date;
        searchListRequest.PublishedAfterDateTimeOffset = indexingContext.ReleasedSince;
        searchListRequest.Q = query;
        if (indexingContext.ReleasedSince.HasValue)
        {
            searchListRequest.PublishedAfterDateTimeOffset = indexingContext.ReleasedSince;
        }

        while (nextPageToken != null && (!result.Any() ||
                                         result.Last().Snippet.PublishedAtDateTimeOffset >=
                                         indexingContext.ReleasedSince))
        {
            SearchListResponse response;
            try
            {
                response = await searchListRequest.ExecuteAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Failed to use {nameof(youTubeService)}.");
                indexingContext.SkipYouTubeUrlResolving = true;
                return result.Select(ToEpisodeResult);
            }

            result.AddRange(response.Items.Where(x =>
                x.Snippet.PublishedAtDateTimeOffset >= indexingContext.ReleasedSince));
            nextPageToken = response.NextPageToken;
            searchListRequest.PageToken = nextPageToken;
        }

        return result.Select(ToEpisodeResult);
    }

    private EpisodeResult ToEpisodeResult(SearchResult episode)
    {
        return new EpisodeResult(
            episode.Id.VideoId,
            episode.Snippet.PublishedAtDateTimeOffset!.Value.UtcDateTime,
            episode.Snippet.Description.Trim(),
            episode.Snippet.Title.Trim(),
            episode.Snippet.ChannelTitle.Trim(),
            DiscoveryService.YouTube,
            episode.ToYouTubeUrl(),
            episode.Snippet.ChannelId);
    }
}