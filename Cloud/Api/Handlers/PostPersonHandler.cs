using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Extensions;
using Api.Models;
using Api.Services.People;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers;

public class PostPersonHandler(
    IPersonUpdateService personUpdateService,
    ILogger<PostPersonHandler> logger) : IPostPersonHandler
{
    public async Task<HttpResponseData> Handle(
        HttpRequestData req,
        PersonChangeRequestWrapper request,
        ClientPrincipal? _,
        CancellationToken c)
    {
        var result = await personUpdateService.UpdateAsync(request, c);

        return result.Status switch
        {
            PersonUpdateStatus.Accepted =>
                req.CreateResponse(HttpStatusCode.Accepted),
            PersonUpdateStatus.NotFound =>
                req.CreateResponse(HttpStatusCode.NotFound),
            PersonUpdateStatus.BadRequest =>
                await req.CreateResponse(HttpStatusCode.BadRequest)
                    .WithJsonBody(new { message = result.Message }, c),
            PersonUpdateStatus.Conflict =>
                await req.CreateResponse(HttpStatusCode.Conflict)
                    .WithJsonBody(new { conflict = result.ConflictName }, c),
            PersonUpdateStatus.Failed =>
                await req.CreateResponse(HttpStatusCode.InternalServerError)
                    .WithJsonBody(SubmitUrlResponse.Failure("Unable to update person"), c),
            _ => await LogAndFail(req, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(HttpRequestData req, CancellationToken c)
    {
        logger.LogError("Person update failed with unexpected status.");
        return await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to update person"), c);
    }
}
