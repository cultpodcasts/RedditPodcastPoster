using Microsoft.Azure.Functions.Worker.Http;
using Api.Dtos;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers;

public interface IPostSubmitUrlHandler
{
    Task<HttpResponseData> Handle(
        HttpRequestData req,
        SubmitUrlRequest submitUrlModel,
        ClientPrincipal? cp,
        CancellationToken c);
}
