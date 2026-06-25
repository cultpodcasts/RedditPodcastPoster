namespace RedditPodcastPoster.PodcastServices.YouTube.Quota;

public static class YouTubeQuotaCosts
{
    public const int DailyLimitPerKey = 10_000;
    public const int SearchList = 100;
    public const int PlaylistItemsList = 1;
    public const int PlaylistsList = 1;
    public const int ChannelsList = 1;
    public const int VideosList = 1;

    public static int GetCost(YouTubeQuotaOperation operation) =>
        operation switch
        {
            YouTubeQuotaOperation.SearchList => SearchList,
            YouTubeQuotaOperation.ChannelsList => ChannelsList,
            YouTubeQuotaOperation.PlaylistItemsList => PlaylistItemsList,
            YouTubeQuotaOperation.PlaylistsList => PlaylistsList,
            YouTubeQuotaOperation.VideosList => VideosList,
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };
}
