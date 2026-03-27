using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface ISubjectRepositoryV2 : IRepository<Subject>, IFilterableRepository<Subject>
{
    Task<Subject?> GetByName(string name);
}
