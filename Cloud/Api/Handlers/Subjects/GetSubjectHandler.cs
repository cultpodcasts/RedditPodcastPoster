using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Extensions;
using Api.Models;
using Api.Services.Subjects;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers.Subjects;

public class GetSubjectHandler(
    ISubjectGetService subjectGetService,
    ILogger<GetSubjectHandler> logger) : IGetSubjectHandler
{
    public async Task<HttpResponseData> Handle(
        HttpRequestData req,
        string subjectName,
        ClientPrincipal? cp,
        CancellationToken c)
    {
        var result = await subjectGetService.GetAsync(subjectName, c);

        return result.Status switch
        {
            SubjectGetStatus.Ok =>
                await req.CreateResponse(HttpStatusCode.OK).WithJsonBody(result.Subject!.ToDto(), c),
            SubjectGetStatus.NotFound =>
                req.CreateResponse(HttpStatusCode.NotFound),
            SubjectGetStatus.Failed =>
                await req.CreateResponse(HttpStatusCode.InternalServerError)
                    .WithJsonBody(SubmitUrlResponse.Failure("Unable to retrieve subject"), c),
            _ => await LogAndFail(req, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(HttpRequestData req, CancellationToken c)
    {
        logger.LogError("Subject get failed with unexpected status.");
        return await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to retrieve subject"), c);
    }
}
