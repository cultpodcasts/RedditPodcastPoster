namespace RedditPodcastPoster.UrlSubmission;

public interface ISubmitResultAdaptor
{
    DiscoverySubmitResultState ToDiscoverySubmitResultState(SubmitResult submitResult);
}