using Microsoft.Azure.Functions.Worker.Http;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers;

public interface IPublishHandler
{
    Task<HttpResponseData> PublishHomepage(HttpRequestData req, ClientPrincipal? cp, CancellationToken c);
}