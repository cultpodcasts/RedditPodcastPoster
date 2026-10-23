using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Languages;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Languages;

public sealed class CatalogueLanguageGate(
    ICatalogueLanguageDetector detector,
    ISupportedLanguageRegistrar registrar) : ICatalogueLanguageGate
{
    public async Task<CatalogueLanguageAdmission> Evaluate(
        CatalogueLanguageTexts texts,
        bool creatingNewParent,
        SubmitOptions options,
        CancellationToken cancellationToken = default)
    {
        if (!options.EnforceLanguageGate)
        {
            return CatalogueLanguageAdmission.Proceed();
        }

        if (options.LanguageChoice == CatalogueLanguageChoice.Cancel)
        {
            return CatalogueLanguageAdmission.NeedsPrompt(new CatalogueLanguageHold(string.Empty, string.Empty, []));
        }

        var detection = detector.Detect(texts);
        if (string.IsNullOrWhiteSpace(detection.Code) || EpisodeLanguageResolution.IsEnglish(detection.Code))
        {
            return CatalogueLanguageAdmission.Proceed();
        }

        var code = detection.Code.Trim().ToLowerInvariant();
        var supported = new HashSet<string>(
            options.SupportedLanguageCodes ?? [],
            StringComparer.OrdinalIgnoreCase);
        var known = supported.Contains(code);

        if (!known && options.LanguageChoice == CatalogueLanguageChoice.Unset)
        {
            return CatalogueLanguageAdmission.NeedsPrompt(new CatalogueLanguageHold(
                code,
                detection.EnglishName ?? code,
                detection.Signals));
        }

        if (options.LanguageChoice == CatalogueLanguageChoice.Continue)
        {
            return CatalogueLanguageAdmission.Proceed();
        }

        if (!known && options.LanguageChoice == CatalogueLanguageChoice.Add)
        {
            var name = detection.EnglishName;
            if (string.IsNullOrWhiteSpace(name) ||
                !NeutralCultureLanguageLookup.TryResolveByEnglishName(name, out var resolved, out var canonical) ||
                !resolved.Equals(code, StringComparison.OrdinalIgnoreCase))
            {
                if (!NeutralCultureLanguageLookup.TryResolveByCode(code, out canonical))
                {
                    throw new InvalidOperationException(
                        $"Detected language '{code}' is not a neutral culture and cannot be added.");
                }
            }

            if (!options.LanguageProbe)
            {
                await registrar.RegisterAsync(canonical, cancellationToken);
            }

            known = true;
        }

        if (!known || !creatingNewParent)
        {
            return CatalogueLanguageAdmission.Proceed();
        }

        var stored = EpisodeLanguageResolution.ToStoredLanguage(code);
        return stored == null
            ? CatalogueLanguageAdmission.Proceed()
            : CatalogueLanguageAdmission.Proceed(stored, setsDefault: true);
    }
}
