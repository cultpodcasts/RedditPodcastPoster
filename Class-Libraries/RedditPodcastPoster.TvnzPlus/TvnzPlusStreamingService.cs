using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.TvnzPlus;

public static class TvnzPlusStreamingService
{
    public const string Key = StreamingServiceKeys.TvnzPlus;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Key,
        "TVNZ+",
        "tvnz-plus",
        true,
        ["tvnz.co.nz"]);
}
