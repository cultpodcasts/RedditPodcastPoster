using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.UrlSubmission.Models;
using Podcast = RedditPodcastPoster.Models.V2.Podcast;

namespace RedditPodcastPoster.UrlSubmission;

public interface IDiscoveryResultProcessor
{
    Task<SubmitResult> CreateSubmitResult(DiscoveryResult discoveryResult, IndexingContext indexingContext,
        SubmitOptions submitOptions, Podcast? spotifyPodcast, Podcast? applePodcast, Podcast? youTubePodcast);
}