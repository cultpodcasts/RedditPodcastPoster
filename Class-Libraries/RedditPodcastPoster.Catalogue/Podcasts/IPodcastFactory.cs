using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Catalogue.Podcasts;

public interface IPodcastFactory
{
    Task<Podcast> Create(string podcastName);
}
