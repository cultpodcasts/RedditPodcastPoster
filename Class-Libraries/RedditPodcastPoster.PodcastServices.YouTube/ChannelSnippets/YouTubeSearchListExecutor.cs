using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;

namespace RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;

public class YouTubeSearchListExecutor(IYouTubeServiceWrapper youTubeServiceWrapper) : IYouTubeSearchListExecutor
{
    public Task<SearchListResponse> ExecuteAsync(YouTubeChannelSearchPageRequest request)
    {
        var searchListRequest = youTubeServiceWrapper.YouTubeService.Search.List("snippet");
        searchListRequest.MaxResults = request.MaxResults;
        searchListRequest.ChannelId = request.ChannelId;
        searchListRequest.Type = "video";
        searchListRequest.SafeSearch = SearchResource.ListRequest.SafeSearchEnum.None;
        searchListRequest.Order = SearchResource.ListRequest.OrderEnum.Date;
        searchListRequest.PublishedAfterDateTimeOffset = request.PublishedAfterDateTimeOffset;
        searchListRequest.PublishedBeforeDateTimeOffset = request.PublishedBeforeDateTimeOffset;
        searchListRequest.PageToken = request.PageToken;
        return searchListRequest.ExecuteAsync();
    }
}
