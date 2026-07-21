using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Persistence.Abstractions.Factories;

public interface IFileRepositoryFactory
{
    IFileRepository Create(string container, bool useEntityFolder);
    IFileRepository Create();
}