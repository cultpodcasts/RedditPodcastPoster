namespace RedditPodcastPoster.Auth0;

public interface IAuth0Client
{
    Task<string> GetClientToken();
}