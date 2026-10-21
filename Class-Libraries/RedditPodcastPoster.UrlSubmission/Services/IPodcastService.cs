using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.UrlSubmission.Services;

public interface IPodcastService
{
    Task<Podcast?> GetPodcastFromEpisodeUrl(Uri url, IndexingContext indexingContext);

    /// <summary>
    /// The owning podcast (same rules as <see cref="GetPodcastFromEpisodeUrl"/>) plus the channel / show ids it
    /// was matched on, so a caller can look for a news organisation or TV show that owns the URL.
    /// </summary>
    Task<RedditPodcastPoster.UrlSubmission.Models.EpisodeUrlOwner> GetEpisodeUrlOwner(Uri url, IndexingContext indexingContext);
}
