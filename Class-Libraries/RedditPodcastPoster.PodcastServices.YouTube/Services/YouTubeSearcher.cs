﻿using System.Net;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Channel;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Factories;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Video;

namespace RedditPodcastPoster.PodcastServices.YouTube.Services;

public class YouTubeSearcher(
    IYouTubeServiceWrapper youTubeService,
    INoRedirectHttpClientFactory httpClientFactory,
    IYouTubeVideoService youTubeVideoService,
    IYouTubeChannelService youTubeChannelService,
    ILogger<YouTubeSearcher> logger) : IYouTubeSearcher
{
    private const long MaxSearchResults = 25;
    private const string ShortUrlPrefix = "https://www.youtube.com/shorts/";

    private const string ShortsConsentPrefix =
        "https://consent.youtube.com/m?continue=https%3A%2F%2Fwww.youtube.com%2Fshorts%2F";

    private readonly HttpClient _httpClient = httpClientFactory.Create();

    public async Task<IList<EpisodeResult>> Search(string query, IndexingContext indexingContext)
    {
        logger.LogInformation($"{nameof(YouTubeSearcher)}.{nameof(Search)}: query: '{query}'.");
        var medium = await Search(query, indexingContext, SearchResource.ListRequest.VideoDurationEnum.Medium);
        var @long = await Search(query, indexingContext, SearchResource.ListRequest.VideoDurationEnum.Long__);
        var episodeResults = medium.Union(@long).Distinct();
        logger.LogInformation(
            $"{nameof(Search)}: Found {episodeResults.Count(x => x.Released >= indexingContext.ReleasedSince)} items from youtube matching query '{query}'.");

        return episodeResults.ToList();
    }

    private async Task<IEnumerable<EpisodeResult>> Search(
        string query,
        IndexingContext indexingContext,
        SearchResource.ListRequest.VideoDurationEnum duration)
    {
        var results = new List<YouTubeItemDetails>();
        var nextPageToken = "";
        var searchListRequest = youTubeService.YouTubeService.Search.List("snippet");
        searchListRequest.MaxResults = MaxSearchResults;
        searchListRequest.PageToken = nextPageToken; // or searchListResponse.NextPageToken if paging
        searchListRequest.Type = "video";
        searchListRequest.SafeSearch = SearchResource.ListRequest.SafeSearchEnum.None;
        searchListRequest.Order = SearchResource.ListRequest.OrderEnum.Date;
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
                logger.LogError(ex,
                    $"Failed to use {nameof(youTubeService.YouTubeService)} obtaining episodes using search-term '{query}'.");
                indexingContext.SkipYouTubeUrlResolving = true;
                return results.Select(x => ToEpisodeResult(x.SearchResult, x.Video, x.Channel));
            }

            var releasedInTimeFrame = response.Items.Where(x =>
                x.Snippet.PublishedAtDateTimeOffset >= indexingContext.ReleasedSince);


            results.AddRange(releasedInTimeFrame.Select(x => new YouTubeItemDetails(x)));
            nextPageToken = response.NextPageToken;
            searchListRequest.PageToken = nextPageToken;
        }

        await EnrichWithVideo(results, indexingContext);
        await EnrichWithChannel(results, indexingContext);

        return results.Select(x => ToEpisodeResult(x.SearchResult, x.Video, x.Channel));
    }

    private async Task EnrichWithVideo(
        List<YouTubeItemDetails> results,
        IndexingContext indexingContext)
    {
        var videoIds = results.Select(x => x.SearchResult.Id.VideoId);
        var videos =
            await youTubeVideoService.GetVideoContentDetails(youTubeService, videoIds, indexingContext, true, true);
        foreach (var result in results)
        {
            var video = videos?.FirstOrDefault(x => x.Id == result.SearchResult.Id.VideoId);
            result.Video = video;
        }
    }

    private async Task EnrichWithChannel(
        List<YouTubeItemDetails> results,
        IndexingContext indexingContext)
    {
        foreach (var result in results)
        {
            var channel = await youTubeChannelService.GetChannel(
                new YouTubeChannelId(result.SearchResult.Snippet.ChannelId),
                indexingContext, withStatistics: true, withSnippets: true);
            result.Channel = channel;
        }
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

    private EpisodeResult ToEpisodeResult(SearchResult episode, Google.Apis.YouTube.v3.Data.Video? video,
        Google.Apis.YouTube.v3.Data.Channel? channel)
    {
        var imageUrl = video.GetImageUrl();

        var episodeResult = new EpisodeResult(
            episode.Id.VideoId,
            episode.Snippet.PublishedAtDateTimeOffset!.Value.UtcDateTime,
            !string.IsNullOrWhiteSpace(video?.Snippet.Description)
                ? WebUtility.HtmlDecode(video?.Snippet.Description!)
                : WebUtility.HtmlDecode(episode.Snippet.Description.Trim()),
            WebUtility.HtmlDecode(episode.Snippet.Title.Trim()),
            video?.GetLength(),
            WebUtility.HtmlDecode(episode.Snippet.ChannelTitle.Trim()),
            channel?.Snippet.Description ?? string.Empty,
            DiscoverService.YouTube,
            video?.Statistics.ViewCount,
            channel?.Statistics.SubscriberCount,
            imageUrl
        );
        episodeResult.Urls.YouTube = episode.ToYouTubeUrl();
        episodeResult.PodcastIds.YouTube = channel?.Id;
        return episodeResult;
    }

    private class YouTubeItemDetails(SearchResult searchResult)
    {
        public SearchResult SearchResult { get; } = searchResult;
        public Google.Apis.YouTube.v3.Data.Video? Video { get; set; }
        public Google.Apis.YouTube.v3.Data.Channel? Channel { get; set; }
    }
}