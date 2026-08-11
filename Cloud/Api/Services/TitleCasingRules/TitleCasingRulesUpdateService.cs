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
            if (normalised == TitleCasingRulesDocument.UniversalLanguageKey)
            {
                return new TitleCasingRulesUpdateResult(
                    TitleCasingRulesUpdateStatus.BadRequest,
                    Error: "Universal language does not store lower-case terms.");
            }

            var document = await GetOrMaterializeLanguageAsync(normalised);
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
            if (normalised == TitleCasingRulesDocument.UniversalLanguageKey)
            {
                return new TitleCasingRulesUpdateResult(
                    TitleCasingRulesUpdateStatus.BadRequest,
                    Error: "Universal language does not store lower-case terms.");
            }

            var document = await GetOrMaterializeLanguageAsync(normalised);
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

    public async Task<TitleCasingRulesUpdateResult> AddIgnoredSubjectAsync(
        string language,
        TitleCasingRulesAddLowerCaseTermRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            var normalisedResult = TryNormaliseNonEnglish(language);
            if (normalisedResult.Error is not null)
            {
                return normalisedResult.Error;
            }

            var document = await GetOrMaterializeNonEnglishAsync(normalisedResult.Language!);
            var mutation = TitleCasingRulesMutationRules.TryAddIgnoredSubject(
                document.IgnoredSubjects,
                body.Term);
            if (!mutation.IsValid)
            {
                return new TitleCasingRulesUpdateResult(
                    TitleCasingRulesUpdateStatus.BadRequest,
                    Error: mutation.Error);
            }

            document.IgnoredSubjects = mutation.Terms.Count == 0 ? null : mutation.Terms.ToArray();
            await titleCasingRulesRepository.Save(document);
            return new TitleCasingRulesUpdateResult(TitleCasingRulesUpdateStatus.Ok, document);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure to add ignored subject for language {Language}.", language);
            return new TitleCasingRulesUpdateResult(TitleCasingRulesUpdateStatus.Failed);
        }
    }

    public async Task<TitleCasingRulesUpdateResult> DeleteIgnoredSubjectAsync(
        string language,
        string term,
        CancellationToken cancellationToken)
    {
        try
        {
            var normalisedResult = TryNormaliseNonEnglish(language);
            if (normalisedResult.Error is not null)
            {
                return normalisedResult.Error;
            }

            var document = await GetOrMaterializeNonEnglishAsync(normalisedResult.Language!);
            var mutation = TitleCasingRulesMutationRules.TryRemoveIgnoredSubject(
                document.IgnoredSubjects,
                term);
            if (!mutation.IsValid)
            {
                return new TitleCasingRulesUpdateResult(
                    TitleCasingRulesUpdateStatus.BadRequest,
                    Error: mutation.Error);
            }

            document.IgnoredSubjects = mutation.Terms.Count == 0 ? null : mutation.Terms.ToArray();
            await titleCasingRulesRepository.Save(document);
            return new TitleCasingRulesUpdateResult(TitleCasingRulesUpdateStatus.Ok, document);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure to delete ignored subject for language {Language}.", language);
            return new TitleCasingRulesUpdateResult(TitleCasingRulesUpdateStatus.Failed);
        }
    }

    private static (string? Language, TitleCasingRulesUpdateResult? Error) TryNormalise(string language)
    {
        var normalised = TitleCasingRulesDocument.NormaliseLanguage(language);
        if (string.IsNullOrEmpty(normalised))
        {
            return (null, new TitleCasingRulesUpdateResult(
                TitleCasingRulesUpdateStatus.BadRequest,
                Error: "Language code must be non-empty."));
        }

        return (normalised, null);
    }

    private static (string? Language, TitleCasingRulesUpdateResult? Error) TryNormaliseNonEnglish(string language)
    {
        var normalisedResult = TryNormalise(language);
        if (normalisedResult.Error is not null)
        {
            return normalisedResult;
        }

        var normalised = normalisedResult.Language!;
        if (normalised == TitleCasingRulesDocument.UniversalLanguageKey ||
            string.Equals(normalised, "en", StringComparison.OrdinalIgnoreCase))
        {
            return (null, new TitleCasingRulesUpdateResult(
                TitleCasingRulesUpdateStatus.BadRequest,
                Error: "Ignored subjects are only stored for non-English languages."));
        }

        return (normalised, null);
    }

    private async Task<TitleCasingRulesDocument> GetOrMaterializeAsync(string normalised)
    {
        var existing = await titleCasingRulesRepository.Get(normalised);
        if (existing is not null)
        {
            return existing;
        }

        return TitleCasingRulesDocument.CreateForLanguage(normalised) switch
        {
            EnglishTitleCasingRulesDocument => await BuildEnglishDefaultAsync(),
            var created => created
        };
    }

    private async Task<LanguageTitleCasingRulesDocument> GetOrMaterializeLanguageAsync(string normalised)
    {
        var document = await GetOrMaterializeAsync(normalised);
        if (document is LanguageTitleCasingRulesDocument languageDocument)
        {
            return languageDocument;
        }

        throw new InvalidOperationException(
            $"Language '{normalised}' does not support lower-case terms.");
    }

    private async Task<NonEnglishTitleCasingRulesDocument> GetOrMaterializeNonEnglishAsync(string normalised)
    {
        var document = await GetOrMaterializeAsync(normalised);
        if (document is NonEnglishTitleCasingRulesDocument nonEnglish)
        {
            return nonEnglish;
        }

        throw new InvalidOperationException(
            $"Language '{normalised}' does not support ignored subjects.");
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
