using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.TitleCasing;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Text.Models;
using KnownTermsModel = RedditPodcastPoster.Text.KnownTerms.KnownTerms;

namespace SeedTitleCasingRules;

public class SeedTitleCasingRulesProcessor(
    ILanguageTitleCasingRulesRepository titleCasingRulesRepository,
    ILookupRepository lookupRepository,
    ILogger<SeedTitleCasingRulesProcessor> logger)
{
    public async Task<int> Run(SeedTitleCasingRulesRequest request)
    {
        var language = LanguageTitleCasingRulesDocument.NormaliseLanguage(request.Language);
        if (string.IsNullOrEmpty(language))
        {
            logger.LogError("Language code is required.");
            return 1;
        }

        var existing = await titleCasingRulesRepository.Get(language);
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

        var lowerCaseTerms = language == "en"
            ? LowerCaseTerms.DefaultEnglishWords.ToList()
            : [];

        var document = new LanguageTitleCasingRulesDocument(language)
        {
            LowerCaseTerms = lowerCaseTerms,
            KnownTerms = knownTerms
        };

        logger.LogInformation(
            "Would seed language '{Language}' id={Id}: {LowerCount} lower-case terms, {KnownCount} known terms. Existing={Exists}. Apply={Apply}.",
            language,
            document.Id,
            document.LowerCaseTerms.Count,
            document.KnownTerms.Count,
            existing is not null,
            request.Apply);

        if (!request.Apply)
        {
            logger.LogInformation("Dry-run only. Pass --apply to write.");
            return 0;
        }

        await titleCasingRulesRepository.Save(document);
        logger.LogInformation("Saved title-casing rules for '{Language}'.", language);
        return 0;
    }
}
