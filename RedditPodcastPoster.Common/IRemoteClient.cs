namespace RedditPodcastPoster.Common;

public interface IRemoteClient
{
    Task<T> InvokeGet<T>(string apiCall);
}