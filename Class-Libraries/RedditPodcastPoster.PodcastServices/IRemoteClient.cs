namespace RedditPodcastPoster.PodcastServices;

public interface IRemoteClient
{
    Task<T> InvokeGet<T>(string apiCall);
}