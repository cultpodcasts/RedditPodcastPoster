namespace RedditPodcastPoster.Auth0;

public interface IAuth0TokenValidator
{
    Task<ValidatedToken?> GetClaimsPrincipalAsync(string auth0Bearer);
}