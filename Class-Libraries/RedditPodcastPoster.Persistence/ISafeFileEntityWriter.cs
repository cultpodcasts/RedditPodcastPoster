using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence;

public interface ISafeFileEntityWriter
{
    Task Write<T>(T data) where T : CosmosSelector;
}