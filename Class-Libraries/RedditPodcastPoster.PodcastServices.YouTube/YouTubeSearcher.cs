using System.Net;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using static Google.Apis.YouTube.v3.SearchResource.ListRequest;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public class YouTubeSearcher(
    YouTubeService youTubeService,
    INoRedirectHttpClientFactory httpClientFactory,
    ILogger<YouTubeSearcher> logger) : IYouTubeSearcher
{
    private const long MaxSearchResults = 25;
    private const string ShortUrlPrefix = "https://www.youtube.com/shorts/";

    private const string ShortsConsentPrefix =
        "https://consent.youtube.com/m?continue=https%3A%2F%2Fwww.youtube.com%2Fshorts%2F";

    private readonly HttpClient _httpClient = httpClientFactory.Create();

    public async Task<IEnumerable<EpisodeResult>> Search(string query, IndexingContext indexingContext)
    {
        var medium = await Search(query, indexingContext, VideoDurationEnum.Medium);
        var @long = await Search(query, indexingContext, VideoDurationEnum.Long__);
        return medium.Union(@long).Distinct();
    }

    private async Task<IEnumerable<EpisodeResult>> Search(string query, IndexingContext indexingContext,
        VideoDurationEnum duration)
    {
        var results = new List<SearchResult>();
        var nextPageToken = "";
        var searchListRequest = youTubeService.Search.List("snippet");
        searchListRequest.MaxResults = MaxSearchResults;
        searchListRequest.PageToken = nextPageToken; // or searchListResponse.NextPageToken if paging
        searchListRequest.Type = "video";
        searchListRequest.SafeSearch = SafeSearchEnum.None;
        searchListRequest.Order = OrderEnum.Date;
        searchListRequest.PublishedAfterDateTimeOffset = indexingContext.ReleasedSince;
        searchListRequest.Q = query;
        searchListRequest.VideoDuration = duration;
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

            var releasedInTimeFrame = response.Items.Where(x =>
                x.Snippet.PublishedAtDateTimeOffset >= indexingContext.ReleasedSince);


            results.AddRange(releasedInTimeFrame);
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
                var location = head.Headers.GetValues("Location").FirstOrDefault();
                if (!location!.StartsWith(ShortsConsentPrefix))
                {
                    nonShortResults.Add(result);
                }
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