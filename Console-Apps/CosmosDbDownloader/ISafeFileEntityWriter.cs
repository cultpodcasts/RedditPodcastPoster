using RedditPodcastPoster.Models;

namespace CosmosDbDownloader;

public interface ISafeFileEntityWriter
{
    Task Write<T>(T data) where T : CosmosSelector;
}