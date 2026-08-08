using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.TitleCasing;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Text.Models;
using KnownTermsModel = RedditPodcastPoster.Text.KnownTerms.KnownTerms;

namespace Api.Services.TitleCasingRules;

public class TitleCasingRulesUpdateService(
    ILanguageTitleCasingRulesRepository titleCasingRulesRepository,
    ILookupRepository lookupRepository,
    ILogger<TitleCasingRulesUpdateService> logger) : ITitleCasingRulesUpdateService
{
    public async Task<TitleCasingRulesUpdateResult> AddLowerCaseTermAsync(
        string language,
        TitleCasingRulesAddLowerCaseTermRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            var normalisedResult = TryNormalise(language);
            if (normalisedResult.Error is not null)
            {
                return normalisedResult.Error;
            }

            var normalised = normalisedResult.Language!;
            if (normalised == LanguageTitleCasingRulesDocument.UniversalLanguageKey)
            {
                return new TitleCasingRulesUpdateResult(
                    TitleCasingRulesUpdateStatus.BadRequest,
                    Error: "Universal language does not store lower-case terms.");
            }

            var document = await GetOrMaterializeAsync(normalised);
            var mutation = TitleCasingRulesMutationRules.TryAddLowerCaseTerm(
                document.LowerCaseTerms,
                body.Term);
            if (!mutation.IsValid)
            {
                return new TitleCasingRulesUpdateResult(
                    TitleCasingRulesUpdateStatus.BadRequest,
                    Error: mutation.Error);
            }

            document.LowerCaseTerms = mutation.Terms.ToList();
            await titleCasingRulesRepository.Save(document);
            return new TitleCasingRulesUpdateResult(TitleCasingRulesUpdateStatus.Ok, document);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure to add lower-case term for language {Language}.", language);
            return new TitleCasingRulesUpdateResult(TitleCasingRulesUpdateStatus.Failed);
        }
    }

    public async Task<TitleCasingRulesUpdateResult> DeleteLowerCaseTermAsync(
        string language,
        string term,
        CancellationToken cancellationToken)
    {
        try
        {
            var normalisedResult = TryNormalise(language);
            if (normalisedResult.Error is not null)
            {
                return normalisedResult.Error;
            }

            var normalised = normalisedResult.Language!;
            if (normalised == LanguageTitleCasingRulesDocument.UniversalLanguageKey)
            {
                return new TitleCasingRulesUpdateResult(
                    TitleCasingRulesUpdateStatus.BadRequest,
                    Error: "Universal language does not store lower-case terms.");
            }

            var document = await GetOrMaterializeAsync(normalised);
            var mutation = TitleCasingRulesMutationRules.TryRemoveLowerCaseTerm(
                document.LowerCaseTerms,
                term);
            if (!mutation.IsValid)
            {
                return new TitleCasingRulesUpdateResult(
                    TitleCasingRulesUpdateStatus.BadRequest,
                    Error: mutation.Error);
            }

            document.LowerCaseTerms = mutation.Terms.ToList();
            await titleCasingRulesRepository.Save(document);
            return new TitleCasingRulesUpdateResult(TitleCasingRulesUpdateStatus.Ok, document);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure to delete lower-case term for language {Language}.", language);
            return new TitleCasingRulesUpdateResult(TitleCasingRulesUpdateStatus.Failed);
        }
    }

    public async Task<TitleCasingRulesUpdateResult> UpsertKnownTermAsync(
        string language,
        KnownTermUpdate body,
        CancellationToken cancellationToken)
    {
        try
        {
            var normalisedResult = TryNormalise(language);
            if (normalisedResult.Error is not null)
            {
                return normalisedResult.Error;
            }

            var normalised = normalisedResult.Language!;
            var document = await GetOrMaterializeAsync(normalised);
            if (normalised == LanguageTitleCasingRulesDocument.UniversalLanguageKey)
            {
                document.LowerCaseTerms = [];
            }

            var mutation = TitleCasingRulesMutationRules.TryUpsertKnownTerm(
                document.KnownTerms,
                body.Literal,
                body.Pattern,
                body.Options);
            if (!mutation.IsValid)
            {
                return new TitleCasingRulesUpdateResult(
                    TitleCasingRulesUpdateStatus.BadRequest,
                    Error: mutation.Error);
            }

            document.KnownTerms = mutation.Terms.ToList();
            await titleCasingRulesRepository.Save(document);
            return new TitleCasingRulesUpdateResult(TitleCasingRulesUpdateStatus.Ok, document);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure to upsert known term for language {Language}.", language);
            return new TitleCasingRulesUpdateResult(TitleCasingRulesUpdateStatus.Failed);
        }
    }

    public async Task<TitleCasingRulesUpdateResult> DeleteKnownTermAsync(
        string language,
        string literal,
        CancellationToken cancellationToken)
    {
        try
        {
            var normalisedResult = TryNormalise(language);
            if (normalisedResult.Error is not null)
            {
                return normalisedResult.Error;
            }

            var normalised = normalisedResult.Language!;
            var document = await GetOrMaterializeAsync(normalised);
            if (normalised == LanguageTitleCasingRulesDocument.UniversalLanguageKey)
            {
                document.LowerCaseTerms = [];
            }

            var mutation = TitleCasingRulesMutationRules.TryRemoveKnownTerm(
                document.KnownTerms,
                literal);
            if (!mutation.IsValid)
            {
                return new TitleCasingRulesUpdateResult(
                    TitleCasingRulesUpdateStatus.BadRequest,
                    Error: mutation.Error);
            }

            document.KnownTerms = mutation.Terms.ToList();
            await titleCasingRulesRepository.Save(document);
            return new TitleCasingRulesUpdateResult(TitleCasingRulesUpdateStatus.Ok, document);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure to delete known term for language {Language}.", language);
            return new TitleCasingRulesUpdateResult(TitleCasingRulesUpdateStatus.Failed);
        }
    }

    private static (string? Language, TitleCasingRulesUpdateResult? Error) TryNormalise(string language)
    {
        var normalised = LanguageTitleCasingRulesDocument.NormaliseLanguage(language);
        if (string.IsNullOrEmpty(normalised))
        {
            return (null, new TitleCasingRulesUpdateResult(
                TitleCasingRulesUpdateStatus.BadRequest,
                Error: "Language code must be non-empty."));
        }

        return (normalised, null);
    }

    /// <summary>
    /// Loads the Cosmos document, or materialises English/Universal defaults so the first delta
    /// cannot wipe registered terms that the admin UI shows from GET isDefault.
    /// </summary>
    private async Task<LanguageTitleCasingRulesDocument> GetOrMaterializeAsync(string normalised)
    {
        var existing = await titleCasingRulesRepository.Get(normalised);
        if (existing is not null)
        {
            return existing;
        }

        if (normalised == LanguageTitleCasingRulesDocument.UniversalLanguageKey)
        {
            return new LanguageTitleCasingRulesDocument(LanguageTitleCasingRulesDocument.UniversalLanguageKey);
        }

        if (normalised == "en")
        {
            return await BuildEnglishDefaultAsync();
        }

        return new LanguageTitleCasingRulesDocument(normalised);
    }

    private async Task<LanguageTitleCasingRulesDocument> BuildEnglishDefaultAsync()
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

        return LanguageTitleCasingRulesDocument.CreateEnglishDefault(
            LowerCaseTerms.DefaultEnglishWords,
            knownTerms);
    }
}
