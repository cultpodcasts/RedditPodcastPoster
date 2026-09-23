using RedditPodcastPoster.Models.News;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

public interface INewsOrganisationRepository : IRepository<NewsOrganisation>, IFilterableRepository<NewsOrganisation>
{
    Task<NewsOrganisation?> GetNewsOrganisation(Guid newsOrganisationId);
}
