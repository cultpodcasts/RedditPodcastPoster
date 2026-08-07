using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.TitleCasing;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Services.TitleCasingRules;

public class TitleCasingRulesUpdateService(
    ILanguageTitleCasingRulesRepository titleCasingRulesRepository,
    ILogger<TitleCasingRulesUpdateService> logger) : ITitleCasingRulesUpdateService
{
    public async Task<TitleCasingRulesUpdateResult> UpdateAsync(
        string language,
        LanguageTitleCasingRulesUpdateRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            var normalised = LanguageTitleCasingRulesDocument.NormaliseLanguage(language);
            if (string.IsNullOrEmpty(normalised))
            {
                return new TitleCasingRulesUpdateResult(
                    TitleCasingRulesUpdateStatus.BadRequest,
                    Error: "Language code must be non-empty.");
            }

            var isUniversal = normalised == LanguageTitleCasingRulesDocument.UniversalLanguageKey;
            var document = new LanguageTitleCasingRulesDocument(normalised);

            if (isUniversal)
            {
                document.LowerCaseTerms = [];
            }
            else if (body.LowerCaseTerms is { Count: > 0 })
            {
                document.LowerCaseTerms = body.LowerCaseTerms
                    .Select(term => term.Trim())
                    .Where(term => term.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            if (body.KnownTerms is { Count: > 0 })
            {
                foreach (var term in body.KnownTerms)
                {
                    var literal = term.Literal?.Trim();
                    if (string.IsNullOrEmpty(literal))
                    {
                        return new TitleCasingRulesUpdateResult(
                            TitleCasingRulesUpdateStatus.BadRequest,
                            Error: "Each known term must have a non-empty literal.");
                    }

                    var pattern = term.Pattern?.Trim();
                    if (string.IsNullOrEmpty(pattern))
                    {
                        return new TitleCasingRulesUpdateResult(
                            TitleCasingRulesUpdateStatus.BadRequest,
                            Error: "Each known term must have a non-empty pattern.");
                    }

                    try
                    {
                        var entry = new KnownTermEntry
                        {
                            Literal = literal,
                            Pattern = pattern,
                            Options = term.Options ??
                                      nameof(System.Text.RegularExpressions.RegexOptions.IgnoreCase) + ", " +
                                      nameof(System.Text.RegularExpressions.RegexOptions.Compiled)
                        };
                        _ = entry.ToRegex();
                        document.KnownTerms.Add(entry);
                    }
                    catch (Exception ex)
                    {
                        return new TitleCasingRulesUpdateResult(
                            TitleCasingRulesUpdateStatus.BadRequest,
                            Error: $"Invalid regex pattern for known term '{literal}': {ex.Message}");
                    }
                }
            }

            await titleCasingRulesRepository.Save(document);
            return new TitleCasingRulesUpdateResult(TitleCasingRulesUpdateStatus.Ok, document);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure to update title-casing rules for language {Language}.", language);
            return new TitleCasingRulesUpdateResult(TitleCasingRulesUpdateStatus.Failed);
        }
    }
}
