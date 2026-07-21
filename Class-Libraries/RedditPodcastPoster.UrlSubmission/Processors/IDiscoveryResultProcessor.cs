using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace RedditPodcastPoster.UrlSubmission.Processors;

public interface IDiscoveryResultProcessor
{
    Task<SubmitResult> CreateSubmitResult(DiscoveryResult discoveryResult, IndexingContext indexingContext,
        SubmitOptions submitOptions, Podcast? spotifyPodcast, Podcast? applePodcast, Podcast? youTubePodcast);
}
