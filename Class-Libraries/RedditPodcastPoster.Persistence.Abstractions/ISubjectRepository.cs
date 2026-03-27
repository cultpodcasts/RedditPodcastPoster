using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface ISubjectRepository : IRepository<Subject>, IFilterableRepository<Subject>
{
    Task<Subject?> GetByName(string name);
}
