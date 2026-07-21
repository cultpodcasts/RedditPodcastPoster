using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

public interface IPodcastRepository : IRepository<Podcast>, IFilterableRepository<Podcast>
{
    Task<Podcast?> GetPodcast(Guid podcastId);
}
