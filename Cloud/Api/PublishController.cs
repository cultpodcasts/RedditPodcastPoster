using System.Net;
using Api.Configuration;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.ContentPublisher;

namespace Api;

public class PublishController(
    IContentPublisher contentPublisher,
    ILogger<PublishController> logger,
    ILogger<BaseHttpFunction> baseLogger,
    IOptions<HostingOptions> hostingOptions)
    : BaseHttpFunction(hostingOptions, baseLogger)
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

    private async Task<HttpResponseData> PublishHomepage(HttpRequestData req, CancellationToken c)
    {
        try
        {
            await contentPublisher.PublishHomepage();
            return req.CreateResponse(HttpStatusCode.OK);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(PublishHomepage)}: Failed to publish homepage.");
        }

        var failure = req.CreateResponse(HttpStatusCode.InternalServerError);
        return failure;
    }
}