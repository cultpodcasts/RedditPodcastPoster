using Podcast = RedditPodcastPoster.Models.V2.Podcast;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface IPodcastRepositoryV2 : IRepository<Podcast>, IFilterableRepository<Podcast>
{
    Task<Podcast?> GetPodcast(Guid podcastId);
}
