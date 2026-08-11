using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.TitleCasing;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Text.Models;
using KnownTermsModel = RedditPodcastPoster.Text.KnownTerms.KnownTerms;

namespace Api.Services.TitleCasingRules;

public class TitleCasingRulesGetService(
    ILanguageTitleCasingRulesRepository titleCasingRulesRepository,
    ILookupRepository lookupRepository,
    ILogger<TitleCasingRulesGetService> logger) : ITitleCasingRulesGetService
{
    public async Task<TitleCasingRulesGetResult> GetAsync(string language, CancellationToken cancellationToken)
    {
        try
        {
            var normalised = TitleCasingRulesDocument.NormaliseLanguage(language);
            if (string.IsNullOrEmpty(normalised))
            {
                return new TitleCasingRulesGetResult(TitleCasingRulesGetStatus.NotFound);
            }

            var persisted = await titleCasingRulesRepository.Get(normalised);
            if (persisted is not null)
            {
                return new TitleCasingRulesGetResult(
                    TitleCasingRulesGetStatus.Ok,
                    persisted,
                    IsDefault: false);
            }

            if (normalised == TitleCasingRulesDocument.UniversalLanguageKey)
            {
                return new TitleCasingRulesGetResult(
                    TitleCasingRulesGetStatus.Ok,
                    new UniversalTitleCasingRulesDocument(),
                    IsDefault: true);
            }

            if (normalised == "en")
            {
                var defaults = await BuildEnglishDefaultAsync();
                return new TitleCasingRulesGetResult(
                    TitleCasingRulesGetStatus.Ok,
                    defaults,
                    IsDefault: true);
            }

            return new TitleCasingRulesGetResult(TitleCasingRulesGetStatus.NotFound);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure to get title-casing rules for language {Language}.", language);
            return new TitleCasingRulesGetResult(TitleCasingRulesGetStatus.Failed);
        }
    }

    private async Task<EnglishTitleCasingRulesDocument> BuildEnglishDefaultAsync()
    {
        var knownTerms = new List<KnownTermEntry>();
        var legacy = await lookupRepository.GetKnownTerms<KnownTermsModel>();
        if (legacy?.Terms is { Count: > 0 })
        {
            knownTerms = legacy.Terms
                .Select(kv => new KnownTermEntry
                {
                    Literal = kv.Key,
                    Pattern = kv.Value.ToString(),
                    Options = kv.Value.Options.ToString()
                })
                .ToList();
        }

        return TitleCasingRulesDocument.CreateEnglishDefault(
            LowerCaseTerms.DefaultEnglishWords,
            knownTerms);
    }
}
