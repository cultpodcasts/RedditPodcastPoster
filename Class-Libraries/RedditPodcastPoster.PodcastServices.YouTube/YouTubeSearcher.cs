using System.Net;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public class YouTubeSearcher(
    YouTubeService youTubeService,
    INoRedirectHttpClientFactory httpClientFactory,
    ILogger<YouTubeSearcher> logger) : IYouTubeSearcher
{
    private const long MaxSearchResults = 25;
    private const string ShortUrlPrefix = "https://www.youtube.com/shorts/";
    private readonly HttpClient _httpClient = httpClientFactory.Create();

    public async Task<IEnumerable<EpisodeResult>> Search(string query, IndexingContext indexingContext)
    {
        var results = new List<SearchResult>();
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

        while (nextPageToken != null && (!results.Any() ||
                                         results.Last().Snippet.PublishedAtDateTimeOffset >=
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
                return results.Select(ToEpisodeResult);
            }

            var nonShortItems = await NonShortItems(response.Items.Where(x =>
                x.Snippet.PublishedAtDateTimeOffset >= indexingContext.ReleasedSince));


            results.AddRange(nonShortItems);
            nextPageToken = response.NextPageToken;
            searchListRequest.PageToken = nextPageToken;
        }

        return results.Select(ToEpisodeResult);
    }

    private async Task<IEnumerable<SearchResult>> NonShortItems(IEnumerable<SearchResult> results)
    {
        var nonShortResults = new List<SearchResult>();
        foreach (var result in results)
        {
            var id = result.Id.VideoId;
            var url = $"{ShortUrlPrefix}{id}";
            var head = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
            if (head.StatusCode == HttpStatusCode.Found)
            {
                nonShortResults.Add(result);
            }
        }

        return nonShortResults;
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