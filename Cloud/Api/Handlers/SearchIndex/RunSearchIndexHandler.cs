using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Extensions;
using Api.Models;
using Api.Services.SearchIndex;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers.SearchIndex;

public class RunSearchIndexHandler(
    ISearchIndexRunService searchIndexRunService,
    ILogger<RunSearchIndexHandler> logger) : IRunSearchIndexHandler
{
    public async Task<HttpResponseData> Handle(HttpRequestData req, ClientPrincipal? cp, CancellationToken c)
    {
        var result = await searchIndexRunService.RunAsync(c);
        return result.Status switch
        {
            SearchIndexRunStatus.Ok =>
                await req.CreateResponse(HttpStatusCode.OK).WithJsonBody(result.Result!.ToDto(), c),
            SearchIndexRunStatus.BadRequest =>
                await req.CreateResponse(HttpStatusCode.BadRequest).WithJsonBody(result.Result!.ToDto(), c),
            SearchIndexRunStatus.Failed =>
                await req.CreateResponse(HttpStatusCode.InternalServerError)
                    .WithJsonBody(ApiErrorResponse.Failure("Unable to update podcast"), c),
            _ => LogAndFail(req)
        };
    }

    private HttpResponseData LogAndFail(HttpRequestData req)
    {
        logger.LogError("Search index run failed with unexpected status.");
        return req.CreateResponse(HttpStatusCode.InternalServerError);
    }
}
