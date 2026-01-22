using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Subjects;

public class SubjectRepository(
    IDataRepository repository,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<SubjectRepository> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : ISubjectRepository
{
    public Task Save(Subject subject)
    {
        return repository.Write(subject);
    }

    public IAsyncEnumerable<Subject> GetAll()
    {
        return repository.GetAll<Subject>();
    }

    public IAsyncEnumerable<T> GetAll<T>(Expression<Func<Subject, T>> item)
    {
        return repository.GetAll(item);
    }

    public IAsyncEnumerable<Subject> GetAllBy(Expression<Func<Subject, bool>> selector)
    {
        return repository.GetAllBy(selector);
    }

    public IAsyncEnumerable<T> GetAllBy<T>(Expression<Func<Subject, bool>> selector, Expression<Func<Subject, T>> item)
    {
        return repository.GetAllBy(selector, item);
    }

    public Task<Subject?> GetByName(string name)
    {
        return repository.GetBy<Subject>(x => x.Name == name);
    }

    public IAsyncEnumerable<Subject> GetByNames(string[] names)
    {
        return repository
            .GetAllBy<Subject>(x => ((IEnumerable<string>)names).Contains(x.Name))
            .OrderBy(s => Array.IndexOf(names, s.Name));
    }

    public Task<Subject?> GetBy(Expression<Func<Subject, bool>> selector)
    {
        return repository.GetBy(selector);
    }
}