using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.ContentPublisher.Publishers;
using RedditPodcastPoster.Models.Languages;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Services.SupportedLanguages;

public class SupportedLanguagesUpdateService(
    ILookupRepository lookupRepository,
    ILanguagesPublisher languagesPublisher,
    ILogger<SupportedLanguagesUpdateService> logger) : ISupportedLanguagesUpdateService
{
    public async Task<SupportedLanguagesUpdateResult> UpdateAsync(
        SupportedLanguagesUpdateRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            if (body.Languages is null || body.Languages.Count == 0)
            {
                return new SupportedLanguagesUpdateResult(
                    SupportedLanguagesUpdateStatus.BadRequest,
                    Error: "languages must contain at least one entry.");
            }

            var languages = new List<SupportedLanguage>();
            var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in body.Languages)
            {
                var code = entry.Code?.Trim();
                var name = entry.Name?.Trim();

                if (string.IsNullOrEmpty(code))
                {
                    return new SupportedLanguagesUpdateResult(
                        SupportedLanguagesUpdateStatus.BadRequest,
                        Error: "Each language must have a non-empty code.");
                }

                if (string.IsNullOrEmpty(name))
                {
                    return new SupportedLanguagesUpdateResult(
                        SupportedLanguagesUpdateStatus.BadRequest,
                        Error: "Each language must have a non-empty name.");
                }

                if (!seenCodes.Add(code))
                {
                    continue;
                }

                languages.Add(new SupportedLanguage
                {
                    Code = code,
                    Name = name
                });
            }

            if (languages.Count == 0)
            {
                return new SupportedLanguagesUpdateResult(
                    SupportedLanguagesUpdateStatus.BadRequest,
                    Error: "languages must contain at least one unique code.");
            }

            var config = await lookupRepository.GetSupportedLanguagesConfig() ?? new SupportedLanguagesConfig();
            config.Languages = languages
                .OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            await lookupRepository.SaveSupportedLanguagesConfig(config);

            var published = await languagesPublisher.PublishLanguages();
            if (!published)
            {
                logger.LogError("SupportedLanguagesConfig saved but R2 languages publish failed.");
                return new SupportedLanguagesUpdateResult(SupportedLanguagesUpdateStatus.Failed);
            }

            return new SupportedLanguagesUpdateResult(SupportedLanguagesUpdateStatus.Ok, config);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure to update SupportedLanguagesConfig.");
            return new SupportedLanguagesUpdateResult(SupportedLanguagesUpdateStatus.Failed);
        }
    }
}
