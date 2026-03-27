using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.V2;

namespace RedditPodcastPoster.Common.Podcasts;

public interface IPodcastFactory
{
    Task<Podcast> Create(string podcastName);
}