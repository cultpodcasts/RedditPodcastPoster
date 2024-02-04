using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Subjects;

public class SubjectRepository(
    IDataRepository repository,
    ILogger<SubjectRepository> logger)
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

    public Task<Subject?> GetByName(string name)
    {
        return repository.GetBy<Subject>(x => x.Name == name);
    }

    public IAsyncEnumerable<Subject> GetByNames(string[] names)
    {
        return repository.GetAllBy<Subject>(x => names.Contains(x.Name));
    }
}