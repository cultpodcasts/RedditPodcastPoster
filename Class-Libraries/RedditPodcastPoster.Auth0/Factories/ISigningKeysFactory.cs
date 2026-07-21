using Microsoft.IdentityModel.Tokens;
using RedditPodcastPoster.DependencyInjection;

namespace RedditPodcastPoster.Auth0.Factories;

public interface ISigningKeysFactory : IAsyncFactory<ICollection<SecurityKey>?>
{
}
