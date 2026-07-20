using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.UrlSubmission;

public interface IUrlSubmitter
{
    Task<SubmitResult> Submit(
        Uri url,
        IndexingContext indexingContext,
        SubmitOptions submitOptions);
}
