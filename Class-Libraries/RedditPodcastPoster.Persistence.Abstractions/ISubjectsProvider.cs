using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface ISubjectsProvider
{
    IAsyncEnumerable<Subject> GetAll();

}