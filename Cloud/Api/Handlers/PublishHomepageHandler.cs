using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Extensions;
using Api.Models;
using Api.Services.Publish;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers;

public class PublishHomepageHandler(
    IHomepagePublishService homepagePublishService,
    ILogger<PublishHomepageHandler> logger) : IPublishHomepageHandler
{
    public async Task<HttpResponseData> Handle(HttpRequestData req, ClientPrincipal? cp, CancellationToken c)
    {
        var result = await homepagePublishService.PublishAsync(c);
        return result.Status switch
        {
            HomepagePublishStatus.Ok =>
                await req.CreateResponse(HttpStatusCode.OK).WithJsonBody(result.Response!, c),
            HomepagePublishStatus.Failed when result.Response != null =>
                await req.CreateResponse(HttpStatusCode.InternalServerError)
                    .WithJsonBody(result.Response, c),
            HomepagePublishStatus.Failed =>
                req.CreateResponse(HttpStatusCode.InternalServerError),
            _ => LogAndFail(req)
        };
    }

    private HttpResponseData LogAndFail(HttpRequestData req)
    {
        logger.LogError("Publish homepage failed with unexpected status.");
        return req.CreateResponse(HttpStatusCode.InternalServerError);
    }
}
