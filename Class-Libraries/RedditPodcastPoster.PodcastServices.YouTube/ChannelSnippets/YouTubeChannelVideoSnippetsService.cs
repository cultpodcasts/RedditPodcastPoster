using System.Net;
using Google;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;

namespace RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets
{
    public class YouTubeChannelVideoSnippetsService(
        ILogger<YouTubeChannelVideoSnippetsService> logger)
        : IYouTubeChannelVideoSnippetsService
    {
        private const int MaxSearchResults = 5;

        public async Task<IList<SearchResult>?> GetLatestChannelVideoSnippets(YouTubeServiceWrapper youTubeServiceWrapper,
            YouTubeChannelId channelId,
            IndexingContext indexingContext)
        {
            var result = new List<SearchResult>();
            var nextPageToken = "";
            var searchListRequest = youTubeServiceWrapper.YouTubeService.Search.List("snippet");
            searchListRequest.MaxResults = MaxSearchResults;
            searchListRequest.ChannelId = channelId.ChannelId;
            searchListRequest.Type = "video";
            searchListRequest.SafeSearch = SearchResource.ListRequest.SafeSearchEnum.None;
            searchListRequest.Order = SearchResource.ListRequest.OrderEnum.Date;
            if (indexingContext.ReleasedSince.HasValue)
            {
                searchListRequest.PublishedAfterDateTimeOffset = indexingContext.ReleasedSince;
            }

            //upcoming
            while (nextPageToken != null)
            {
                searchListRequest.PageToken = nextPageToken; // or searchListResponse.NextPageToken if paging

                SearchListResponse response;
                try
                {
                    response = await searchListRequest.ExecuteAsync();
                }
                catch (GoogleApiException ex)
                {
                    if (ex.HttpStatusCode == HttpStatusCode.Forbidden && ex.Message.Contains("exceeded") &&
                        ex.Message.Contains("quota"))
                    {
                        throw new YouTubeQuotaException();
                    }

                    logger.LogError(ex,
                        $"Unrecognised google-api-exception. Failed to use {nameof(youTubeServiceWrapper.YouTubeService)} with api-key-name '{youTubeServiceWrapper.ApiKeyName}' to obtain latest-channel-snippets for channel-id '{channelId.ChannelId}'.");
                    indexingContext.SkipYouTubeUrlResolving = true;
                    return result;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        $"Failed to use {nameof(youTubeServiceWrapper.YouTubeService)} with api-key-name '{youTubeServiceWrapper.ApiKeyName}' to obtain latest-channel-snippets for channel-id '{channelId.ChannelId}'.");
                    indexingContext.SkipYouTubeUrlResolving = true;
                    return result;
                }

                var responseItems = response.Items.Where(x =>
                    x.Snippet.LiveBroadcastContent != "upcoming" && x.Snippet.LiveBroadcastContent != "live");
                result.AddRange(responseItems);
                nextPageToken = response.NextPageToken;
            }

            return result;
        }
    }
}