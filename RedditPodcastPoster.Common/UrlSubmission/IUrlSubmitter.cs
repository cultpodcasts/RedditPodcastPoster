using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.UrlSubmission;

public interface IUrlSubmitter
{
    Task Submit(IList<Podcast> podcasts, Uri url, IndexingContext indexingContext, bool searchForPodcast);
}