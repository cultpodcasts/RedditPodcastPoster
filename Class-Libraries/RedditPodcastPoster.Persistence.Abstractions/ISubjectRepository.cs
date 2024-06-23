using System.Linq.Expressions;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface ISubjectRepository
{
    Task Save(Subject subject);
    IAsyncEnumerable<Subject> GetAll();
    Task<Subject?> GetByName(string name);
    IAsyncEnumerable<Subject> GetByNames(string[] names);
    Task<Subject?> GetBy(Expression<Func<Subject, bool>> selector);
}