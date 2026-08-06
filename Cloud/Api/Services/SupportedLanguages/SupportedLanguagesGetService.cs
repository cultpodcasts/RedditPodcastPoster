using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Languages;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Services.SupportedLanguages;

public class SupportedLanguagesGetService(
    ILookupRepository lookupRepository,
    ILogger<SupportedLanguagesGetService> logger) : ISupportedLanguagesGetService
{
    public async Task<SupportedLanguagesGetResult> GetAsync(CancellationToken cancellationToken)
    {
        try
        {
            var persisted = await lookupRepository.GetSupportedLanguagesConfig();
            var config = persisted ?? SupportedLanguagesConfig.CreateDefault();
            return new SupportedLanguagesGetResult(
                SupportedLanguagesGetStatus.Ok,
                config,
                IsDefault: persisted is null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure to get SupportedLanguagesConfig.");
            return new SupportedLanguagesGetResult(SupportedLanguagesGetStatus.Failed);
        }
    }
}
