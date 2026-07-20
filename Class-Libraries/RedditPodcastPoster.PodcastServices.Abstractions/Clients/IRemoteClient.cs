namespace RedditPodcastPoster.PodcastServices.Abstractions.Clients;

public interface IRemoteClient
{
    Task<T> InvokeGet<T>(string apiCall);
}