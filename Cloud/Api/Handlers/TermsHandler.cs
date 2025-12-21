using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Api.Dtos;
using Api.Extensions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Auth0;
using RedditPodcastPoster.Text.KnownTerms;

namespace Api.Handlers;

public class TermsHandler(
    IKnownTermsRepository knownTermsRepository,
    ILogger<TermsHandler> logger) : ITermsHandler
{
    public async Task<HttpResponseData> Post(HttpRequestData r, TermSubmitRequest req, ClientPrincipal? _,
        CancellationToken c)
    {
        try
        {
            var knownTerms = await knownTermsRepository.Get();
            if (knownTerms.Terms.Keys.Select(x => x.ToLowerInvariant())
                .Contains(Regex.Escape(req.Term).ToLowerInvariant()))
            {
                return r.CreateResponse(HttpStatusCode.Conflict);
            }

            var titleCasedTerm = Regex.Escape(new CultureInfo("en-GB", false).TextInfo.ToTitleCase(req.Term));
            if (!titleCasedTerm.StartsWith("("))
            {
                titleCasedTerm = @$"\b{titleCasedTerm}";
            }

            if (!titleCasedTerm.EndsWith(")"))
            {
                titleCasedTerm = @$"{titleCasedTerm}\b";
            }

            knownTerms.Terms.Add(req.Term,
                new Regex(titleCasedTerm, RegexOptions.Compiled | RegexOptions.IgnoreCase));
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