using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface ICachedSubjectsProvider: ISubjectsProvider{}

public interface ISubjectsProvider
{
    IAsyncEnumerable<Subject> GetAll();

}