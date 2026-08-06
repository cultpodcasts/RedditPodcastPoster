using Api.Models;

namespace Api.Services.SupportedLanguages;

public interface ISupportedLanguagesUpdateService
{
    Task<SupportedLanguagesUpdateResult> UpdateAsync(
        SupportedLanguagesUpdateRequest body,
        CancellationToken cancellationToken);
}
