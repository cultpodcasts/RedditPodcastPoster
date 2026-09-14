using RedditPodcastPoster.AmazonPrime.Matching;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.AmazonPrime;

public static class AmazonPrimeStreamingService
{
    public static readonly StreamingService Service = StreamingService.AmazonPrime;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Service,
        tryResolve: url => AmazonPrimeUrlMatcher.IsCatalogUrl(url) ? StreamingServiceWire.ToKey(Service) : null);
}
