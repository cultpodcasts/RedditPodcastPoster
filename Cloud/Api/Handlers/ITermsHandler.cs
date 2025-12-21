using Api.Dtos;
using Microsoft.Azure.Functions.Worker.Http;
using RedditPodcastPoster.Auth0;

namespace Api.Handlers;

public interface ITermsHandler
{
    Task<HttpResponseData> Post(HttpRequestData req, TermSubmitRequest termSubmitRequest,
        ClientPrincipal? clientPrincipal, CancellationToken c);
}