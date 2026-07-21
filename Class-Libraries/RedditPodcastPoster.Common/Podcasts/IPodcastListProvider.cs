using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Common.Podcasts;

public interface IPodcastListProvider
{
    Task<IEnumerable<Podcast>> GetRemotePodcasts();
}
