using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Podcasts;

public interface IPodcastListProvider
{
    Task<IEnumerable<Podcast>> GetRemotePodcasts();
}