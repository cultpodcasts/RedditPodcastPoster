using System.Linq.Expressions;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface ISubjectRepositoryV2
{
    Task Save(Subject subject);
    IAsyncEnumerable<Subject> GetAll();
    Task<Subject?> GetByName(string name);
    Task<Subject?> GetBy(Expression<Func<Subject, bool>> selector);
    IAsyncEnumerable<Subject> GetAllBy(Expression<Func<Subject, bool>> selector);
}
