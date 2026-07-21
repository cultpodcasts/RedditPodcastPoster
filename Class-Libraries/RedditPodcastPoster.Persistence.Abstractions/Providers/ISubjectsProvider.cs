using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions.Providers;

public interface ISubjectsProvider
{
    IAsyncEnumerable<Subject> GetAll();
}