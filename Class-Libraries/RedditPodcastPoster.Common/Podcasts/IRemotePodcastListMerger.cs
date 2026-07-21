using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Common.Podcasts;

public interface IRemotePodcastListMerger
{
    Task<IEnumerable<Podcast>> GetMergedPodcastList();
}
