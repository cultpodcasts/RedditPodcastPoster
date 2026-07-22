using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Extensions;
using Api.Models;
using Api.Services.People;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers;

public class GetAllPeopleHandler(
    IPersonGetAllService personGetAllService,
    ILogger<GetAllPeopleHandler> logger) : IGetAllPeopleHandler
{
    public async Task<HttpResponseData> Handle(HttpRequestData req, ClientPrincipal? _, CancellationToken c)
    {
        var result = await personGetAllService.GetAllAsync(c);

        return result.Status switch
        {
            PersonGetAllStatus.Ok =>
                await req.CreateResponse(HttpStatusCode.OK).WithJsonBody(result.People!, c),
            PersonGetAllStatus.Failed =>
                await req.CreateResponse(HttpStatusCode.InternalServerError)
                    .WithJsonBody(SubmitUrlResponse.Failure("Unable to retrieve people"), c),
            _ => await LogAndFail(req, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(HttpRequestData req, CancellationToken c)
    {
        logger.LogError("People get-all failed with unexpected status.");
        return await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to retrieve people"), c);
    }
}
