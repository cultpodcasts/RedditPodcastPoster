using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface ISubjectRepository
{
    Task<IEnumerable<Subject>> GetAll();
    Task<Subject?> GetByName(string name);
}