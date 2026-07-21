using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Subjects;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

public interface ISubjectRepository : IRepository<Subject>, IFilterableRepository<Subject>
{
    Task<Subject?> GetByName(string name);
}
