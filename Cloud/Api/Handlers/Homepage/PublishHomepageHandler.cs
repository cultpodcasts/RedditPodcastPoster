using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Extensions;
using Api.Models;
using Api.Services.Homepage;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers.Homepage;

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
                await req.CreateResponse(HttpStatusCode.OK)
                    .WithJsonBody(PublishHomepageResponse.ToDto(result.Result!), c),
            HomepagePublishStatus.Failed when result.Result != null =>
                await req.CreateResponse(HttpStatusCode.InternalServerError)
                    .WithJsonBody(PublishHomepageResponse.ToDto(result.Result), c),
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
