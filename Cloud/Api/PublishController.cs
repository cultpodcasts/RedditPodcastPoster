using System.Net;
using Api.Configuration;
using Api.Dtos;
using Api.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Auth0;
using RedditPodcastPoster.ContentPublisher;

namespace Api;

public class PublishController(
    IContentPublisher contentPublisher,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<PublishController> logger,
    IOptions<HostingOptions> hostingOptions)
    : BaseHttpFunction(clientPrincipalFactory, hostingOptions, logger)
{
    [Function("PublishHomepage")]
    public Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "publish/homepage")]
        HttpRequestData req,
        FunctionContext executionContext,
        CancellationToken ct
    )
    {
        return HandleRequest(req, ["admin"], PublishHomepage, Unauthorised, ct);
    }

    private async Task<HttpResponseData> PublishHomepage(HttpRequestData req, ClientPrincipal? _, CancellationToken c)
    {
        try
        {
            var result = await contentPublisher.PublishHomepage();
            if (!result.HomepagePublished || (result.PreProcessedHomepagePublished.HasValue &&
                                              !result.PreProcessedHomepagePublished.Value))
            {
                logger.LogError("{method}: Failed to publish homepage. Result: {result}",
                    nameof(PublishHomepage), result);
                return await req.CreateResponse(HttpStatusCode.InternalServerError)
                    .WithJsonBody(PublishHomepageResponse.ToDto(result), c);
            }

            return await req.CreateResponse(HttpStatusCode.OK).WithJsonBody(PublishHomepageResponse.ToDto(result), c);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to publish homepage.", nameof(PublishHomepage));
        }

        var failure = req.CreateResponse(HttpStatusCode.InternalServerError);
        return failure;
    }
}