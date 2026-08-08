using Microsoft.IdentityModel.Tokens;
using RedditPodcastPoster.DependencyInjection;

namespace RedditPodcastPoster.Auth0.Warmup;

/// <summary>
/// Warms Auth0 OIDC signing keys (see <c>IAsyncInstance&lt;ICollection&lt;SecurityKey&gt;?&gt;</c>).
/// </summary>
public sealed class Auth0SigningKeysStartupWarmer(
    IAsyncInstance<ICollection<SecurityKey>?> signingKeys) : IStartupWarmer
{
    public string Name => "Auth0SigningKeys";

    public Task WarmAsync(CancellationToken cancellationToken) => signingKeys.GetAsync(cancellationToken);
}
