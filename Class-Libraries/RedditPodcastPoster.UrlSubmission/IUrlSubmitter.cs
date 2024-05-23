using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.UrlSubmission;

public interface IUrlSubmitter
{
    Task<SubmitResult> Submit(
        Uri url,
        IndexingContext indexingContext,
        SubmitOptions submitOptions);
}