using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Api.Auth;
using Api.Configuration;
using Api.Dtos;
using Api.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Text.KnownTerms;

namespace Api;

public class TermsController(
    IKnownTermsRepository knownTermsRepository,
    ILogger<DiscoveryCurationController> logger,
    ILogger<BaseHttpFunction> baseLogger,
    IOptions<HostingOptions> hostingOptions)
    : BaseHttpFunction(hostingOptions, baseLogger)
{
    private const string? Route = "terms";


    [Function("TermPost")]
    public Task<HttpResponseData> Post(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = Route)]
        HttpRequestData req,
        FunctionContext executionContext,
        [FromBody] TermSubmitRequest termSubmitRequest,
        CancellationToken ct)
    {
        return HandleRequest(req, ["curate"], termSubmitRequest, Post, Unauthorised, ct);
    }

    private async Task<HttpResponseData> Post(HttpRequestData r, TermSubmitRequest t, ClientPrincipal? _, CancellationToken c)
    {
        try
        {
            var knownTerms = await knownTermsRepository.Get();
            if (knownTerms.Terms.Keys.Select(x => x.ToLowerInvariant()).Contains(t.Term.ToLowerInvariant()))
            {
                return r.CreateResponse(HttpStatusCode.Conflict);
            }

            var titleCasedTerm = new CultureInfo("en-GB", false).TextInfo.ToTitleCase(t.Term);
            knownTerms.Terms.Add(t.Term,
                new Regex(@$"\b{titleCasedTerm}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase));
            await knownTermsRepository.Save(knownTerms);
            return await r.CreateResponse(HttpStatusCode.OK).WithJsonBody(new { }, c);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure to submit term.");
            return r.CreateResponse(HttpStatusCode.InternalServerError);
        }
    }
}