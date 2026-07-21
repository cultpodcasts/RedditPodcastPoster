using RedditPodcastPoster.Auth0.Models;

namespace RedditPodcastPoster.Auth0.Validators;

public interface IAuth0TokenValidator
{
    Task<ValidatedToken?> GetClaimsPrincipalAsync(string auth0Bearer);
}
