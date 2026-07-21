using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Writers;

public interface ISafeFileEntityWriter
{
    Task Write<T>(T data) where T : CosmosSelector;
}