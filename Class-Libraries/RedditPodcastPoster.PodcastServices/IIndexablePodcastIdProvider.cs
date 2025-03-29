namespace RedditPodcastPoster.PodcastServices;

public interface IIndexablePodcastIdProvider
{
    IAsyncEnumerable<Guid> GetIndexablePodcastIds();
}