using System.Linq.Expressions;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface ISubjectRepository : ISubjectsProvider
{
    Task Save(Subject subject);
    IAsyncEnumerable<T> GetAll<T>(Expression<Func<Subject, T>> item);
    Task<Subject?> GetByName(string name);
    IAsyncEnumerable<Subject> GetByNames(string[] names);
    Task<Subject?> GetBy(Expression<Func<Subject, bool>> selector);
    IAsyncEnumerable<Subject> GetAllBy(Expression<Func<Subject, bool>> selector);
    IAsyncEnumerable<T> GetAllBy<T>(Expression<Func<Subject, bool>> selector, Expression<Func<Subject, T>> item);

}