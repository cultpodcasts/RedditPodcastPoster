using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Extensions;
using Api.Models;
using Api.Services.People;
using PersonDto = Api.Dtos.Person;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers.People;

public class PutPersonHandler(
    IPersonCreateService personCreateService,
    ILogger<PutPersonHandler> logger) : IPutPersonHandler
{
    public async Task<HttpResponseData> Handle(
        HttpRequestData req,
        PersonDto person,
        ClientPrincipal? _,
        CancellationToken ct)
    {
        var result = await personCreateService.CreateAsync(person, ct);

        return result.Status switch
        {
            PersonCreateStatus.Accepted =>
                await req.CreateResponse(HttpStatusCode.Accepted).WithJsonBody(result.Person!.ToDto(), ct),
            PersonCreateStatus.BadRequest =>
                await req.CreateResponse(HttpStatusCode.BadRequest)
                    .WithJsonBody(new { message = result.Message }, ct),
            PersonCreateStatus.Conflict =>
                await req.CreateResponse(HttpStatusCode.Conflict)
                    .WithJsonBody(new { conflict = result.ConflictName }, ct),
            PersonCreateStatus.Failed =>
                await req.CreateResponse(HttpStatusCode.InternalServerError)
                    .WithJsonBody(SubmitUrlResponse.Failure("Unable to create person"), ct),
            _ => await LogAndFail(req, ct)
        };
    }

    private async Task<HttpResponseData> LogAndFail(HttpRequestData req, CancellationToken c)
    {
        logger.LogError("Person create failed with unexpected status.");
        return await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to create person"), c);
    }
}
