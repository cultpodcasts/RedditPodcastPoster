using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Extensions;
using Api.Models;
using Api.Services.Subjects;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers;

public class PutSubjectHandler(
    ISubjectCreateService subjectCreateService,
    ILogger<PutSubjectHandler> logger) : IPutSubjectHandler
{
    public async Task<HttpResponseData> Handle(
        HttpRequestData req,
        Subject subject,
        ClientPrincipal? cp,
        CancellationToken ct)
    {
        var result = await subjectCreateService.CreateAsync(subject, ct);

        return result.Status switch
        {
            SubjectCreateStatus.Accepted =>
                await req.CreateResponse(HttpStatusCode.Accepted).WithJsonBody(result.Subject!, ct),
            SubjectCreateStatus.BadRequest =>
                await req.CreateResponse(HttpStatusCode.BadRequest)
                    .WithJsonBody(new { message = result.Message }, ct),
            SubjectCreateStatus.Conflict =>
                await req.CreateResponse(HttpStatusCode.Conflict)
                    .WithJsonBody(new { conflict = result.ConflictName }, ct),
            SubjectCreateStatus.Failed =>
                await req.CreateResponse(HttpStatusCode.InternalServerError)
                    .WithJsonBody(SubmitUrlResponse.Failure("Unable to create subject"), ct),
            _ => await LogAndFail(req, ct)
        };
    }

    private async Task<HttpResponseData> LogAndFail(HttpRequestData req, CancellationToken ct)
    {
        logger.LogError("Subject create failed with unexpected status.");
        return await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to create subject"), ct);
    }
}
