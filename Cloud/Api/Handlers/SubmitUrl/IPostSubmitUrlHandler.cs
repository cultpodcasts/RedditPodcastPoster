using Microsoft.Azure.Functions.Worker.Http;
using Api.Dtos;
using Api.Models;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers.SubmitUrl;

public interface IPostSubmitUrlHandler
{
    Task<HttpResponseData> Handle(
        HttpRequestData req,
        SubmitUrlRequest submitUrlModel,
        ClientPrincipal? cp,
        CancellationToken c);
}
