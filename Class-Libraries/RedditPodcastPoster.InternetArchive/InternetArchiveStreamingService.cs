using RedditPodcastPoster.InternetArchive.Matching;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.InternetArchive;

public static class InternetArchiveStreamingService
{
    public static readonly StreamingService Service = StreamingService.InternetArchive;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Service,
        tryCompact: InternetArchiveUrlMatcher.TryCompactPayload,
        tryExpand: InternetArchiveUrlMatcher.TryExpandPayload);
}
