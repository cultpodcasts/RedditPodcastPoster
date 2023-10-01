namespace RedditPodcastPoster.Common.UrlSubmission;

public interface IUrlSubmitter
{
    Task Submit(Uri url, IndexOptions indexOptions);
}