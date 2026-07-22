using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Extensions;
using Api.Models;
using Api.Services.People;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers.People;

public class GetPersonHandler(
    IPersonGetService personGetService,
    ILogger<GetPersonHandler> logger) : IGetPersonHandler
{
    public async Task<HttpResponseData> Handle(
        HttpRequestData req,
        string personName,
        ClientPrincipal? _,
        CancellationToken c)
    {
        var result = await personGetService.GetAsync(personName, c);

        return result.Status switch
        {
            PersonGetStatus.Ok =>
                await req.CreateResponse(HttpStatusCode.OK).WithJsonBody(result.Person!.ToDto(), c),
            PersonGetStatus.NotFound =>
                req.CreateResponse(HttpStatusCode.NotFound),
            PersonGetStatus.Failed =>
                await req.CreateResponse(HttpStatusCode.InternalServerError)
                    .WithJsonBody(SubmitUrlResponse.Failure("Unable to retrieve person"), c),
            _ => await LogAndFail(req, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(HttpRequestData req, CancellationToken c)
    {
        logger.LogError("Person get failed with unexpected status.");
        return await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to retrieve person"), c);
    }
}
