using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common;

public interface IPodcastUpdater
{
    Task Update(Podcast podcast, IndexOptions indexOptions);
}