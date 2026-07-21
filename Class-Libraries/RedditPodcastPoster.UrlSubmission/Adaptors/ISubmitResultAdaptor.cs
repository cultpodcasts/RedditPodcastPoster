using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Adaptors;

public interface ISubmitResultAdaptor
{
    DiscoverySubmitResultState ToDiscoverySubmitResultState(SubmitResult submitResult);
}