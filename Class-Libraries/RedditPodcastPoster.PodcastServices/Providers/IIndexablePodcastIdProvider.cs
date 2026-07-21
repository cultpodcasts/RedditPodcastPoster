namespace RedditPodcastPoster.PodcastServices.Providers;

public interface IIndexablePodcastIdProvider
{
    IAsyncEnumerable<Guid> GetIndexablePodcastIds();
}
