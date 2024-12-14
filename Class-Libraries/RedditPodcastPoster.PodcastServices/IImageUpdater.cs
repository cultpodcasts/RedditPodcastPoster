
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices;

public interface IImageUpdater
{
    Task<bool> UpdateImages(Podcast podcast, Episode episode, EpisodeImageUpdateRequest updateRequest);
}

