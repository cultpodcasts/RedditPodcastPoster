using Google.Apis.YouTube.v3.Data;

namespace RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;

public interface IYouTubeSearchListExecutor
{
    Task<SearchListResponse> ExecuteAsync(YouTubeChannelSearchPageRequest request);
}
