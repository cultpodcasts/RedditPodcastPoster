using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Extensions;
using Api.Models;
using Api.Services.SubmitUrl;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers.SubmitUrl;

public class PostSubmitUrlHandler(
    ISubmitUrlService submitUrlService,
    ILogger<PostSubmitUrlHandler> logger) : IPostSubmitUrlHandler
{
    public async Task<HttpResponseData> Handle(
        HttpRequestData req,
        SubmitUrlRequest submitUrlModel,
        ClientPrincipal? cp,
        CancellationToken c)
    {
        var result = await submitUrlService.SubmitAsync(submitUrlModel, c);
        return result.Status switch
        {
            SubmitUrlStatus.Ok =>
                await req.CreateResponse(HttpStatusCode.OK)
                    .WithJsonBody(SubmitUrlResponse.Successful(result.Result!), c),
            SubmitUrlStatus.PodcastNotFound =>
                await req.CreateResponse(HttpStatusCode.NotFound)
                    .WithJsonBody(new { message = result.Message }, c),
            SubmitUrlStatus.Failed =>
                await req.CreateResponse(HttpStatusCode.InternalServerError)
                    .WithJsonBody(SubmitUrlResponse.Failure(result.Message ?? "Failure"), c),
            _ => LogAndFail(req)
        };
    }

    private HttpResponseData LogAndFail(HttpRequestData req)
    {
        logger.LogError("Submit url failed with unexpected status.");
        return req.CreateResponse(HttpStatusCode.InternalServerError);
    }
}
