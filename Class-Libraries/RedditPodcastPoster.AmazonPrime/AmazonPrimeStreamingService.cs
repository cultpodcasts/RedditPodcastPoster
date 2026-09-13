using RedditPodcastPoster.AmazonPrime.Matching;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.AmazonPrime;

public static class AmazonPrimeStreamingService
{
    public const string Key = StreamingServiceKeys.AmazonPrime;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Key,
        "Amazon Prime Video",
        "amazon-prime",
        true,
        ["primevideo.com", "amazon.com", "amazon.co.uk"],
        tryResolve: url => AmazonPrimeUrlMatcher.IsCatalogUrl(url) ? Key : null);
}
