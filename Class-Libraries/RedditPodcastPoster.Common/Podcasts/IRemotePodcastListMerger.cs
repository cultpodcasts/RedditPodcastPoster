using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Podcasts;

public interface IRemotePodcastListMerger
{
    Task<IEnumerable<Podcast>> GetMergedPodcastList();
}