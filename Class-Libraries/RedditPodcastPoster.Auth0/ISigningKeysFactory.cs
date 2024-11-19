using Microsoft.IdentityModel.Tokens;

namespace RedditPodcastPoster.Auth0
{
    public interface ISigningKeysFactory
    {
        Task<ICollection<SecurityKey>?> GetSecurityKeys();
    }
}