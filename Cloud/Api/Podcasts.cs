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
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")]
        HttpRequestData req,
        FunctionContext executionContext,
        CancellationToken ct
    )
    {
        return await HandleRequest(
            req,
            ["submit"],
            async (r, c) =>
            {
                try
                {
                    var podcasts = podcastRepository.GetAllBy(
                        podcast => !podcast.Removed.IsDefined() || podcast.Removed == false,
                        podcast => new {id = podcast.Id, name = podcast.Name});

                    var success = await req.CreateResponse(HttpStatusCode.OK)
                        .WithJsonBody(await podcasts.ToListAsync(c), c);
                    return success;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"{nameof(Run)}: Failed to get-podcasts.");
                }

                var failure = await req.CreateResponse(HttpStatusCode.InternalServerError)
                    .WithJsonBody(SubmitUrlResponse.Failure("Unable to retrieve podcasts"), c);
                return failure;
            },
            async (r, c) =>
            {
                var failure = await req.CreateResponse(HttpStatusCode.Forbidden)
                    .WithJsonBody(SubmitUrlResponse.Failure("Unauthorised"), c);
                return failure;
            },
            ct);
    }
}