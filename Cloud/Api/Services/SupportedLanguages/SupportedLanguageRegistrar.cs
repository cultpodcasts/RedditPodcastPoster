using Api.Models;
using RedditPodcastPoster.UrlSubmission.Languages;

namespace Api.Services.SupportedLanguages;

/// <summary>
/// Submit/discovery "Yes, add this language" uses the same mutation as the admin supported-languages dialog.
/// </summary>
public class SupportedLanguageRegistrar(ISupportedLanguagesUpdateService updates) : ISupportedLanguageRegistrar
{
    public async Task RegisterAsync(string englishName, CancellationToken cancellationToken = default)
    {
        var result = await updates.AddAsync(new SupportedLanguageAddRequest { Name = englishName }, cancellationToken);
        if (result.Status == SupportedLanguagesUpdateStatus.Ok)
        {
            return;
        }

        throw new CatalogueLanguageRegistrationException(
            result.Error ?? "Could not add the supported language.");
    }
}
