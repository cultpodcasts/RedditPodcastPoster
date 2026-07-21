using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

public interface IFileRepository : IDataRepository
{
    string GetFilePath<T>(T data) where T : CosmosSelector;
}