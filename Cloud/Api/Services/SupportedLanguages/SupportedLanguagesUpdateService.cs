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
            if (body.Languages is null)
            {
                return new SupportedLanguagesUpdateResult(
                    SupportedLanguagesUpdateStatus.BadRequest,
                    Error: "languages must contain at least one entry.");
            }

            var existing = await lookupRepository.GetSupportedLanguagesConfig() ?? new SupportedLanguagesConfig();
            var proposed = body.Languages
                .Select(entry => new SupportedLanguageProposal(entry.Code, entry.Name))
                .ToList();

            var validation = SupportedLanguagesUpdateRules.ValidateAndBuild(proposed);

            if (!validation.IsValid)
            {
                return new SupportedLanguagesUpdateResult(
                    SupportedLanguagesUpdateStatus.BadRequest,
                    Error: validation.Error);
            }

            existing.Languages = validation.Languages.ToList();
            await lookupRepository.SaveSupportedLanguagesConfig(existing);

            var published = await languagesPublisher.PublishLanguages();
            if (!published)
            {
                logger.LogError("SupportedLanguagesConfig saved but R2 languages publish failed.");
                return new SupportedLanguagesUpdateResult(SupportedLanguagesUpdateStatus.Failed);
            }

            return new SupportedLanguagesUpdateResult(SupportedLanguagesUpdateStatus.Ok, existing);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure to update SupportedLanguagesConfig.");
            return new SupportedLanguagesUpdateResult(SupportedLanguagesUpdateStatus.Failed);
        }
    }
}
