using System.Net;
using Api.Dtos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Persistence.Abstractions;

namespace Api;

public class Podcasts(
    IPodcastRepository podcastRepository,
    ILogger<Podcasts> logger,
    IOptions<HostingOptions> hostingOptions)
    : BaseHttpFunction(hostingOptions)
{
    [Function("Podcasts")]
    public Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")]
        HttpRequestData req,
        FunctionContext executionContext,
        CancellationToken ct
    )
    {
        return HandleRequest(req, ["submit"], Get, Unauthorised, ct);
    }

    private async Task<HttpResponseData> Get(HttpRequestData req, CancellationToken c)
    {
        try
        {
            var podcasts = podcastRepository.GetAllBy(
                podcast => !podcast.Removed.IsDefined() || podcast.Removed == false,
                podcast => new {id = podcast.Id, name = podcast.Name});

            var podcastResults = await podcasts.ToListAsync(c);
            var success = await req.CreateResponse(HttpStatusCode.OK)
                .WithJsonBody(podcastResults, c);
            return success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(Run)}: Failed to get-podcasts.");
        }

        var failure = await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to retrieve podcasts"), c);
        return failure;
    }
}