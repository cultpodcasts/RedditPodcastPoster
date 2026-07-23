using System.Globalization;
using System.Text.RegularExpressions;
using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Text.KnownTerms;

namespace Api.Services.Terms;

public class TermsSubmitService(
    ILookupRepository lookupRepository,
    ILogger<TermsSubmitService> logger) : ITermsSubmitService
{
    public async Task<TermsSubmitResult> SubmitAsync(
        TermSubmitRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var knownTerms = await lookupRepository.GetKnownTerms<KnownTerms>() ?? new KnownTerms();
            if (knownTerms.Terms.Keys.Select(x => x.ToLowerInvariant())
                .Contains(Regex.Escape(request.Term).ToLowerInvariant()))
            {
                return new TermsSubmitResult(TermsSubmitStatus.Conflict);
            }

            var titleCasedTerm = Regex.Escape(new CultureInfo("en-GB", false).TextInfo.ToTitleCase(request.Term));
            if (!titleCasedTerm.StartsWith("("))
            {
                titleCasedTerm = @$"\b{titleCasedTerm}";
            }

            if (!titleCasedTerm.EndsWith(")"))
            {
                titleCasedTerm = @$"{titleCasedTerm}\b";
            }

            knownTerms.Terms.Add(request.Term,
                new Regex(titleCasedTerm, RegexOptions.Compiled | RegexOptions.IgnoreCase));
            await lookupRepository.SaveKnownTerms(knownTerms);
            return new TermsSubmitResult(TermsSubmitStatus.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure to submit term.");
            return new TermsSubmitResult(TermsSubmitStatus.Failed);
        }
    }
}
