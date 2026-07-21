using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Subjects;

namespace RedditPodcastPoster.Persistence.Abstractions.Providers;

public interface ISubjectsProvider
{
    IAsyncEnumerable<Subject> GetAll();
}