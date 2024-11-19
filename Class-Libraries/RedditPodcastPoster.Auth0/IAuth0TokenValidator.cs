using System.Diagnostics;

namespace RedditPodcastPoster.Auth0;

public interface IAuth0TokenValidator
{
    ValidatedToken? GetClaimsPrincipal(string auth0Bearer);
}

public static class ValidatedTokenExtensions {
    private const string ClaimsRolesIdentifierType = "https://api.cultpodcasts.com/roles";
    public static ClientPrincipal ToClientPrincipal(this ValidatedToken validatedToken)
    {
   

        return new ClientPrincipal
        {
            Claims = validatedToken.ClaimsPrincipal.Claims.Select(x=>new ClientPrincipalClaim()
            {
                Type = x.Type,
                Value = x.Value
            })

        };
    }
}