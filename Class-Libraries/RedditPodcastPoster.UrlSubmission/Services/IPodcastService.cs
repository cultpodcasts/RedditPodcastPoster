using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.UrlSubmission.Services;

public interface IPodcastService
{
    Task<Podcast?> GetPodcastFromEpisodeUrl(Uri url, IndexingContext indexingContext);
}
