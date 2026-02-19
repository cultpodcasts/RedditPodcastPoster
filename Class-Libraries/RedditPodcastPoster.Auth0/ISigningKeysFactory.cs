using Microsoft.IdentityModel.Tokens;
using RedditPodcastPoster.DependencyInjection;

namespace RedditPodcastPoster.Auth0
{
    public interface ISigningKeysFactory : IAsyncFactory<ICollection<SecurityKey>?>
    {
        Task<ICollection<SecurityKey>?> GetSecurityKeys();
    }
}