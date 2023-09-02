using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Podcasts;

public interface IPodcastUpdater
{
    Task Update(Podcast podcast, IndexOptions indexOptions);
}