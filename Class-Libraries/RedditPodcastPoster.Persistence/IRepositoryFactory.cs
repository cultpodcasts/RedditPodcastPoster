using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence;

public interface IRepositoryFactory
{
    IRepository<T> Create<T>(string container) where T : CosmosSelector;
}