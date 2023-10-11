using RedditPodcastPoster.Models;

namespace TextClassifierTraining;

public interface IRepositoryFactory
{
    IRepository<T> Create<T>(string container) where T : CosmosSelector;
}