using Api.Models;

namespace Api.Services.SupportedLanguages;

public interface ISupportedLanguagesUpdateService
{
    Task<SupportedLanguagesUpdateResult> AddAsync(
        SupportedLanguageAddRequest body,
        CancellationToken cancellationToken);

    Task<SupportedLanguagesUpdateResult> DeleteAsync(
        string code,
        CancellationToken cancellationToken);
}
