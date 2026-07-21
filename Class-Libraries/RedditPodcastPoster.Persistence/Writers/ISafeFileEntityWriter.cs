using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Persistence.Writers;

public interface ISafeFileEntityWriter
{
    Task Write<T>(T data) where T : CosmosSelector;
}