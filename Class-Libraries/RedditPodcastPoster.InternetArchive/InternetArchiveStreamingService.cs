using RedditPodcastPoster.InternetArchive.Matching;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.InternetArchive;

public static class InternetArchiveStreamingService
{
    public const string Key = StreamingServiceKeys.InternetArchive;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Key,
        "Internet Archive",
        "internet-archive",
        true,
        ["archive.org"],
        tryCompact: InternetArchiveUrlMatcher.TryCompactPayload,
        tryExpand: InternetArchiveUrlMatcher.TryExpandPayload);
}
