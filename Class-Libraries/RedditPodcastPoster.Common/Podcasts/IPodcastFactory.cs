using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Common.Podcasts;

public interface IPodcastFactory
{
    Task<Podcast> Create(string podcastName);
}
