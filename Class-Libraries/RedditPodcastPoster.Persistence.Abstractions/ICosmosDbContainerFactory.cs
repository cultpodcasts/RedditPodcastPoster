using Microsoft.Azure.Cosmos;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface ICosmosDbContainerFactory
{
    Container Create();
}