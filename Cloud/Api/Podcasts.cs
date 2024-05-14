using System.Net;
using Api.Dtos;
using Api.Extensions;
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
        if (!req.HasScope("submit"))
        {
            try
            {
                var podcasts = podcastRepository.GetAllBy(
                    podcast => !podcast.Removed.IsDefined() || podcast.Removed == false ,
                    podcast => new  {id= podcast.Id, name=podcast.Name});


                var success = req.CreateResponse(HttpStatusCode.OK);
                await success.WriteAsJsonAsync(await podcasts.ToListAsync(ct), ct);
                return success;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"{nameof(Run)}: Failed to get-podcasts.");
            }

            var failure = req.CreateResponse(HttpStatusCode.BadRequest);
            await failure.WriteAsJsonAsync(SubmitUrlResponse.Failure("Unable to retrieve podcasts"), ct);
            return failure;
        }
        else
        {
            var failure = req.CreateResponse(HttpStatusCode.Forbidden);
            await failure.WriteAsJsonAsync(SubmitUrlResponse.Failure(), ct);
            return failure;
        }
    }
}

public class SimplePodcast()
{
    public Guid PodcastId { get; set; }
    public string Name { get; set; } 

}