using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.UrlSubmission;

public interface IDiscoveryResultProcessor
{
    Task<SubmitResult> CreateSubmitResult(DiscoveryResult discoveryResult, IndexingContext indexingContext,
        SubmitOptions submitOptions, Podcast? spotifyPodcast, Podcast? applePodcast, Podcast? youTubePodcast);
}