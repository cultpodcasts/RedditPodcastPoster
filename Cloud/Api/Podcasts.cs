using System.Net;
using Api.Dtos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;

namespace Api;

public class Podcasts(IPodcastRepository podcastRepository, ILogger<Podcasts> logger)
{
    [Function("Podcasts")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")]
        HttpRequestData req,
        FunctionContext executionContext,
        CancellationToken ct
    )
    {
        return await req.HandleRequest(
            new[] {"submit"},
            async (r, c) =>
            {
                try
                {
                    var podcasts = podcastRepository.GetAllBy(
                        podcast => !podcast.Removed.IsDefined() || podcast.Removed == false,
                        podcast => new {id = podcast.Id, name = podcast.Name});

                    var success = req.CreateResponse(HttpStatusCode.OK);
                    await success.WriteAsJsonAsync(await podcasts.ToListAsync(c), c);
                    return success;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"{nameof(Run)}: Failed to get-podcasts.");
                }

                var failure = req.CreateResponse(HttpStatusCode.InternalServerError);
                await failure.WriteAsJsonAsync(SubmitUrlResponse.Failure("Unable to retrieve podcasts"), c);
                return failure;
            },
            async (r, c) =>
            {
                var failure = req.CreateResponse(HttpStatusCode.Forbidden);
                await failure.WriteAsJsonAsync(SubmitUrlResponse.Failure("Unauthorised"), ct);
                return failure;
            },
            ct);
    }
}