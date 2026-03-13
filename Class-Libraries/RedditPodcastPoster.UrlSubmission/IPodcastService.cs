using RedditPodcastPoster.Models.V2;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.UrlSubmission;

public interface IPodcastService
{
    Task<Podcast?> GetPodcastFromEpisodeUrl(Uri url, IndexingContext indexingContext);
}