using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface IFileRepository : IDataRepository
{
    string GetFilePath<T>(T data) where T : CosmosSelector;
}