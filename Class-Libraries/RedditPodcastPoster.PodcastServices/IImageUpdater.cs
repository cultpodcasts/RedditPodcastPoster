
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices;

public interface IImageUpdater
{
    Task<bool> UpdateImages(Podcast podcast, Episode episode, EpisodeImageUpdateRequest updateRequest, IndexingContext indexingContext);
}

