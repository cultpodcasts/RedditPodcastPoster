using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Extensions;
using Api.Models;
using Api.Services.Terms;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers;

public class PostTermsHandler(
    ITermsSubmitService termsSubmitService,
    ILogger<PostTermsHandler> logger) : IPostTermsHandler
{
    public async Task<HttpResponseData> Handle(
        HttpRequestData req,
        TermSubmitRequest termSubmitRequest,
        ClientPrincipal? clientPrincipal,
        CancellationToken c)
    {
        var result = await termsSubmitService.SubmitAsync(termSubmitRequest, c);
        return result.Status switch
        {
            TermsSubmitStatus.Ok =>
                await req.CreateResponse(HttpStatusCode.OK).WithJsonBody(new { }, c),
            TermsSubmitStatus.Conflict =>
                req.CreateResponse(HttpStatusCode.Conflict),
            TermsSubmitStatus.Failed =>
                req.CreateResponse(HttpStatusCode.InternalServerError),
            _ => LogAndFail(req)
        };
    }

    private HttpResponseData LogAndFail(HttpRequestData req)
    {
        logger.LogError("Terms submit failed with unexpected status.");
        return req.CreateResponse(HttpStatusCode.InternalServerError);
    }
}
