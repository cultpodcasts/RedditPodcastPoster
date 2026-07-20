using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.UrlSubmission;

public interface IPodcastService
{
    Task<Podcast?> GetPodcastFromEpisodeUrl(Uri url, IndexingContext indexingContext);
}
