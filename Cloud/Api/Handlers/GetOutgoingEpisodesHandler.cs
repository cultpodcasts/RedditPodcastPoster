using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Extensions;
using Api.Models;
using Api.Services.Episodes;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers;

public class GetOutgoingEpisodesHandler(
    IEpisodeOutgoingService episodeOutgoingService,
    ILogger<GetOutgoingEpisodesHandler> logger) : IGetOutgoingEpisodesHandler
{
    public async Task<HttpResponseData> Handle(
        HttpRequestData req,
        ClientPrincipal? cp,
        CancellationToken c)
    {
        var query = OutgoingEpisodesQuery.Parse(
            req.Query["days"],
            req.Query["posted"],
            req.Query["tweeted"],
            req.Query["blueskyPosted"]);

        var result = await episodeOutgoingService.GetOutgoingAsync(query, c);

        return result.Status switch
        {
            EpisodeOutgoingStatus.Ok =>
                await req.CreateResponse(HttpStatusCode.OK).WithJsonBody(result.Episodes!, c),
            EpisodeOutgoingStatus.Failed =>
                await req.CreateResponse(HttpStatusCode.InternalServerError)
                    .WithJsonBody(SubmitUrlResponse.Failure("Unable to retrieve out-going episodes"), c),
            _ => await LogAndFail(req, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(HttpRequestData req, CancellationToken c)
    {
        logger.LogError("Outgoing episodes get failed with unexpected status.");
        return await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to retrieve out-going episodes"), c);
    }
}
