using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Mapping;
using Api.Extensions;
using Api.Models;
using Api.Services.Episodes;
using RedditPodcastPoster.Auth0.Models;
using RedditPodcastPoster.Subjects.Providers;

namespace Api.Handlers.Episodes;

public class GetOutgoingEpisodesHandler(
    IEpisodeOutgoingService episodeOutgoingService,
    ICachedSubjectProvider subjectsProvider,
    EpisodeDiscreteMapper episodeDiscreteMapper,
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

        if (result.Status == EpisodeOutgoingStatus.Failed)
        {
            return await req.CreateResponse(HttpStatusCode.InternalServerError)
                .WithJsonBody(SubmitUrlResponse.Failure("Unable to retrieve out-going episodes"), c);
        }

        if (result.Status != EpisodeOutgoingStatus.Ok)
        {
            return await LogAndFail(req, c);
        }

        var subjects = await subjectsProvider.GetAll().ToListAsync(c);
        var episodes = new List<DiscreteEpisode>();
        foreach (var pair in result.Episodes!)
        {
            episodes.Add(await episodeDiscreteMapper.ToDiscreteEpisode(pair.Episode, pair.Podcast, subjects));
        }

        return await req.CreateResponse(HttpStatusCode.OK).WithJsonBody(episodes, c);
    }

    private async Task<HttpResponseData> LogAndFail(HttpRequestData req, CancellationToken c)
    {
        logger.LogError("Outgoing episodes get failed with unexpected status.");
        return await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to retrieve out-going episodes"), c);
    }
}
