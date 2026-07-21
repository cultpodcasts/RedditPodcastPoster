namespace RedditPodcastPoster.Auth0.Clients;

public interface IAuth0Client
{
    Task<string> GetClientToken();
}
