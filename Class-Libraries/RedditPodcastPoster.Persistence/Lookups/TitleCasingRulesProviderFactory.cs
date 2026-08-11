using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.TitleCasing;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Text.Models;
using RedditPodcastPoster.Text.TitleCasing;
using KnownTermsModel = RedditPodcastPoster.Text.KnownTerms.KnownTerms;

namespace RedditPodcastPoster.Persistence.Lookups;

public class TitleCasingRulesProviderFactory(
    ILanguageTitleCasingRulesRepository titleCasingRulesRepository,
    ILookupRepository lookupRepository,
    ILogger<TitleCasingRulesProviderFactory> logger)
    : ITitleCasingRulesProviderFactory
{
    public async Task<ITitleCasingRulesProvider> Create(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("{Method}: Creating {Provider}.", nameof(Create), nameof(TitleCasingRulesProvider));

        var byLanguage = new Dictionary<string, TitleCasingRulesDocument>(StringComparer.OrdinalIgnoreCase);

        // Hot path: point-read universal (*) and English in parallel.
        var universalTask = titleCasingRulesRepository.Get(TitleCasingRulesDocument.UniversalLanguageKey);
        var englishTask = titleCasingRulesRepository.Get("en");
        await Task.WhenAll(universalTask, englishTask).WaitAsync(cancellationToken);

        AddIfPresent(byLanguage, await universalTask);
        var english = await englishTask;
        if (english is not null)
        {
            byLanguage["en"] = english;
        }
        else
        {
            // No English document: scan remaining languages (skip * already loaded), then seed en.
            await foreach (var document in titleCasingRulesRepository.GetAll().WithCancellation(cancellationToken))
            {
                var key = TitleCasingRulesDocument.NormaliseLanguage(document.Language);
                if (string.IsNullOrEmpty(key) ||
                    key == TitleCasingRulesDocument.UniversalLanguageKey)
                {
                    continue;
                }

                byLanguage[key] = document;
            }

            byLanguage["en"] = await BuildEnglishDefaultAsync(cancellationToken);
        }

        return new TitleCasingRulesProvider(
            byLanguage,
            loadLanguage: (language, ct) => titleCasingRulesRepository.Get(language));
    }

    private static void AddIfPresent(
        IDictionary<string, TitleCasingRulesDocument> byLanguage,
        TitleCasingRulesDocument? document)
    {
        if (document is null)
        {
            return;
        }

        var key = TitleCasingRulesDocument.NormaliseLanguage(document.Language);
        if (!string.IsNullOrEmpty(key))
        {
            byLanguage[key] = document;
        }
    }

    private async Task<EnglishTitleCasingRulesDocument> BuildEnglishDefaultAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
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
