using Api.Models;

namespace Api.Services.SupportedLanguages;

public interface ISupportedLanguagesGetService
{
    Task<SupportedLanguagesGetResult> GetAsync(CancellationToken cancellationToken);
}
