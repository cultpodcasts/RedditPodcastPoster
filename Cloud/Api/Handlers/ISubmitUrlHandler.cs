using Microsoft.Azure.Functions.Worker.Http;
using Api.Dtos;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers;

public interface ISubmitUrlHandler
{
    Task<HttpResponseData> Post(
        HttpRequestData req,
        SubmitUrlRequest submitUrlModel,
        ClientPrincipal? cp,
        CancellationToken c);
}