using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission;

public interface ISubmitResultAdaptor
{
    DiscoverySubmitResultState ToDiscoverySubmitResultState(SubmitResult submitResult);
}