using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.UrlSubmission;

public interface IDiscoveryUrlSubmitter
{
    Task<DiscoverySubmitResult> Submit(
        DiscoveryResult discoveryResult,
        IndexingContext indexingContext,
        SubmitOptions submitOptions);
}
