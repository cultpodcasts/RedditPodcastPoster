namespace RedditPodcastPoster.PodcastServices.Abstractions;

public interface IRemoteClient
{
    Task<T> InvokeGet<T>(string apiCall);
}