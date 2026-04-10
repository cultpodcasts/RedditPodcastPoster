using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface IPodcastRepository : IRepository<Podcast>, IFilterableRepository<Podcast>
{
    Task<Podcast?> GetPodcast(Guid podcastId);
}
