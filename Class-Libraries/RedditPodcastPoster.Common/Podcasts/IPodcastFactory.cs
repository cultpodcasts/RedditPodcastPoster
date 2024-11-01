using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Podcasts;

public interface IPodcastFactory
{
    Task<Podcast> Create(string podcastName);
}