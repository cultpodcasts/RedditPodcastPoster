using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Extensions;
using Api.Models;
using Api.Services.Podcasts;
using PodcastRenameRequest = Api.Models.PodcastRenameRequest;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers.Podcasts;

public class RenamePodcastHandler(
    IPodcastRenameService podcastRenameService,
    ILogger<RenamePodcastHandler> logger) : IRenamePodcastHandler
{
    public async Task<HttpResponseData> Handle(
        HttpRequestData req,
        PodcastRenameRequest change,
        ClientPrincipal? _,
        CancellationToken c)
    {
        var result = await podcastRenameService.RenameAsync(change, c);

        return result.Status switch
        {
            PodcastRenameStatus.Ok =>
                await req.CreateResponse(HttpStatusCode.OK)
                    .WithJsonBody(result.SearchIndexer!.ToPodcastRenameResponse(), c),
            PodcastRenameStatus.Conflict =>
                req.CreateResponse(HttpStatusCode.Conflict),
            PodcastRenameStatus.NotFound =>
                req.CreateResponse(HttpStatusCode.NotFound),
            PodcastRenameStatus.BadRequest =>
                req.CreateResponse(HttpStatusCode.BadRequest),
            PodcastRenameStatus.InvalidName =>
                req.CreateResponse(HttpStatusCode.InternalServerError),
            PodcastRenameStatus.TooMany =>
                req.CreateResponse(HttpStatusCode.InternalServerError),
            PodcastRenameStatus.Failed =>
                await req.CreateResponse(HttpStatusCode.InternalServerError)
                    .WithJsonBody(SubmitUrlResponse.Failure("Unable to rename podcast"), c),
            _ => await LogAndFail(req, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(HttpRequestData req, CancellationToken c)
    {
        logger.LogError("Podcast rename failed with unexpected status.");
        return await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to rename podcast"), c);
    }
}
