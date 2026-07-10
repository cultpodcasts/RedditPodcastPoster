using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface IPersonRepository : IRepository<Person>, IFilterableRepository<Person>
{
    Task<Person?> GetByName(string name);
}
