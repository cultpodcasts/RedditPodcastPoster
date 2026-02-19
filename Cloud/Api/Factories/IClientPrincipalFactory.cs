using Microsoft.Azure.Functions.Worker.Http;
using RedditPodcastPoster.Auth0;

namespace Api.Factories;

public interface IClientPrincipalFactory
{
    Task<ClientPrincipal?> CreateAsync(HttpRequestData request);
}