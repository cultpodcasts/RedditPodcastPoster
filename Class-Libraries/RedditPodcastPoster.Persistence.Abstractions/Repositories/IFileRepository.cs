using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

public interface IFileRepository : IDataRepository
{
    string GetFilePath<T>(T data) where T : CosmosSelector;
}