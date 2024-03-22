using System.Net;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using static Google.Apis.YouTube.v3.SearchResource.ListRequest;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public class YouTubeSearcher(
    YouTubeService youTubeService,
    INoRedirectHttpClientFactory httpClientFactory,
    IYouTubeVideoService youTubeVideoService,
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
        var results = new List<(SearchResult SearchResult, Video? Video)>();
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
                                         results.Last().SearchResult.Snippet.PublishedAtDateTimeOffset >=
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
                return results.Select(x => ToEpisodeResult(x.SearchResult, x.Video));
            }

            var releasedInTimeFrame = response.Items.Where(x =>
                x.Snippet.PublishedAtDateTimeOffset >= indexingContext.ReleasedSince);


            results.AddRange(releasedInTimeFrame.Select(x => (x, (Video?) null)));
            nextPageToken = response.NextPageToken;
            searchListRequest.PageToken = nextPageToken;
        }

        results = await EnrichWithVideo(results, indexingContext);

        return results.Select(x => ToEpisodeResult(x.SearchResult, x.Video));
    }

    private async Task<List<(SearchResult SearchResult, Video? Video)>> EnrichWithVideo(
        List<(SearchResult SearchResult, Video? Video)> results,
        IndexingContext indexingContext)
    {
        var enriched = new List<(SearchResult SearchResult, Video? Video)>();
        var videoIds = results.Select(x => x.SearchResult.Id.VideoId);
        var videos = await youTubeVideoService.GetVideoContentDetails(videoIds, indexingContext, true);
        foreach (var result in results)
        {
            var video = videos?.FirstOrDefault(x => x.Id == result.SearchResult.Id.VideoId);
            enriched.Add((result.SearchResult, video));
        }

        return enriched;
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

    private EpisodeResult ToEpisodeResult(SearchResult episode, Video? video)
    {
        return new EpisodeResult(
            episode.Id.VideoId,
            episode.Snippet.PublishedAtDateTimeOffset!.Value.UtcDateTime,
            !string.IsNullOrWhiteSpace(video?.Snippet.Description)
                ? WebUtility.HtmlDecode(video?.Snippet.Description!)
                : WebUtility.HtmlDecode(episode.Snippet.Description.Trim()),
            WebUtility.HtmlDecode(episode.Snippet.Title.Trim()),
            video?.GetLength(),
            WebUtility.HtmlDecode(episode.Snippet.ChannelTitle.Trim()),
            DiscoveryService.YouTube,
            episode.ToYouTubeUrl(),
            episode.Snippet.ChannelId);
    }
}