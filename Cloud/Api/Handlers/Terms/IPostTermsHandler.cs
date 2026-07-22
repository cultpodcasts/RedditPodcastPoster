using Microsoft.Azure.Functions.Worker.Http;
using Api.Dtos;
using Api.Models;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers.Terms;

public interface IPostTermsHandler
{
    Task<HttpResponseData> Handle(
        HttpRequestData req,
        TermSubmitRequest termSubmitRequest,
        ClientPrincipal? clientPrincipal,
        CancellationToken c);
}
