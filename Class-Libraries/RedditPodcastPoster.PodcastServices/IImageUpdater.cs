
using RedditPodcastPoster.Models.V2;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices;

public interface IImageUpdater
{
    Task<bool> UpdateImages(Podcast podcast, Episode episode, EpisodeImageUpdateRequest updateRequest, IndexingContext indexingContext);
}

