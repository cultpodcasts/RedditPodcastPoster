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

            return await SaveAndPublishAsync(existing, validation.Languages);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure to update SupportedLanguagesConfig.");
            return new SupportedLanguagesUpdateResult(SupportedLanguagesUpdateStatus.Failed);
        }
    }

    public async Task<SupportedLanguagesUpdateResult> AddAsync(
        SupportedLanguageAddRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            var existing = await lookupRepository.GetSupportedLanguagesConfig() ?? new SupportedLanguagesConfig();
            var validation = SupportedLanguagesMutationRules.TryAdd(existing.Languages, body.Name);

            if (!validation.IsValid)
            {
                return new SupportedLanguagesUpdateResult(
                    SupportedLanguagesUpdateStatus.BadRequest,
                    Error: validation.Error);
            }

            return await SaveAndPublishAsync(existing, validation.Languages);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure to add supported language.");
            return new SupportedLanguagesUpdateResult(SupportedLanguagesUpdateStatus.Failed);
        }
    }

    public async Task<SupportedLanguagesUpdateResult> DeleteAsync(
        string code,
        CancellationToken cancellationToken)
    {
        try
        {
            var existing = await lookupRepository.GetSupportedLanguagesConfig() ?? new SupportedLanguagesConfig();
            var validation = SupportedLanguagesMutationRules.TryRemove(existing.Languages, code);

            if (!validation.IsValid)
            {
                return new SupportedLanguagesUpdateResult(
                    SupportedLanguagesUpdateStatus.BadRequest,
                    Error: validation.Error);
            }

            return await SaveAndPublishAsync(existing, validation.Languages);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure to delete supported language '{Code}'.", code);
            return new SupportedLanguagesUpdateResult(SupportedLanguagesUpdateStatus.Failed);
        }
    }

    private async Task<SupportedLanguagesUpdateResult> SaveAndPublishAsync(
        SupportedLanguagesConfig existing,
        IReadOnlyList<SupportedLanguage> languages)
    {
        existing.Languages = languages.ToList();
        await lookupRepository.SaveSupportedLanguagesConfig(existing);

        var published = await languagesPublisher.PublishLanguages();
        if (!published)
        {
            logger.LogError("SupportedLanguagesConfig saved but R2 languages publish failed.");
            return new SupportedLanguagesUpdateResult(SupportedLanguagesUpdateStatus.Failed);
        }

        return new SupportedLanguagesUpdateResult(SupportedLanguagesUpdateStatus.Ok, existing);
    }
}
