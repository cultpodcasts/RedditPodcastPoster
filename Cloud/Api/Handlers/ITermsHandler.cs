using Microsoft.Azure.Functions.Worker.Http;
using Api.Dtos;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers;

public interface ITermsHandler
{
    Task<HttpResponseData> Post(HttpRequestData req, TermSubmitRequest termSubmitRequest,
        ClientPrincipal? clientPrincipal, CancellationToken c);
}