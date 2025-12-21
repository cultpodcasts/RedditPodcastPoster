using Api.Dtos;
using Microsoft.Azure.Functions.Worker.Http;
using RedditPodcastPoster.Auth0;

namespace Api.Handlers;

public interface ISubmitUrlHandler
{
    Task<HttpResponseData> Post(
        HttpRequestData req,
        SubmitUrlRequest submitUrlModel,
        ClientPrincipal? cp,
        CancellationToken c);
}