using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Extensions;
using Api.Models;
using Api.Services.Subjects;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers;

public class PostSubjectHandler(
    ISubjectUpdateService subjectUpdateService,
    ILogger<PostSubjectHandler> logger) : IPostSubjectHandler
{
    public async Task<HttpResponseData> Handle(
        HttpRequestData req,
        SubjectChangeRequestWrapper subjectChangeRequestWrapper,
        ClientPrincipal? cp,
        CancellationToken c)
    {
        var result = await subjectUpdateService.UpdateAsync(subjectChangeRequestWrapper, c);

        return result.Status switch
        {
            SubjectUpdateStatus.Accepted =>
                req.CreateResponse(HttpStatusCode.Accepted),
            SubjectUpdateStatus.NotFound =>
                req.CreateResponse(HttpStatusCode.NotFound),
            SubjectUpdateStatus.Failed =>
                await req.CreateResponse(HttpStatusCode.InternalServerError)
                    .WithJsonBody(SubmitUrlResponse.Failure("Unable to update subject"), c),
            _ => await LogAndFail(req, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(HttpRequestData req, CancellationToken c)
    {
        logger.LogError("Subject update failed with unexpected status.");
        return await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to update subject"), c);
    }
}
