using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Models;

namespace RedditPodcastPoster.PodcastServices.Updaters;

public interface IImageUpdater
{
    Task<bool> UpdateImages(Podcast podcast, Episode episode, EpisodeImageUpdateRequest updateRequest, IndexingContext indexingContext);
}
