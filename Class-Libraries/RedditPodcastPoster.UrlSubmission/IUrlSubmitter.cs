using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.UrlSubmission;

public interface IUrlSubmitter
{
    Task Submit(
        Uri url,
        IndexingContext indexingContext,
        bool searchForPodcast,
        bool matchOtherServices,
        Guid? podcastId,
        SubmitOptions submitOptions);
}