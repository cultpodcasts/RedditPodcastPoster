using Microsoft.Azure.Functions.Worker.Http;
using RedditPodcastPoster.Auth0;

namespace Api;

public interface IClientPrincipalFactory
{
    public ClientPrincipal? Create(HttpRequestData request);
}