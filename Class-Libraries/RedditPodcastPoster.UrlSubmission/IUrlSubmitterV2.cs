using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission;

/// <summary>
/// V2 version of IUrlSubmitter that submits URLs using detached episode repositories.
/// </summary>
public interface IUrlSubmitterV2
{
    /// <summary>
    /// Submits a URL for processing, creating or updating podcasts and episodes via V2 repositories.
    /// </summary>
    Task<SubmitResult> Submit(
        Uri url,
        IndexingContext indexingContext,
        SubmitOptions submitOptions);
}
