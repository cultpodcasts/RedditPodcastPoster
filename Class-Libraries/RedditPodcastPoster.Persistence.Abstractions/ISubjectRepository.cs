using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface ISubjectRepository
{
    Task Save(Subject subject);
    IAsyncEnumerable<Subject> GetAll();
    Task<Subject?> GetByName(string name);
    IAsyncEnumerable<Subject> GetByNames(IList<string> names);
}