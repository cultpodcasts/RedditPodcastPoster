using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

public interface IPersonRepository : IRepository<Person>, IFilterableRepository<Person>
{
    Task<Person?> GetByName(string name);
}
