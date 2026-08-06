using Api.Models;

namespace Api.Services.TitleCasingRules;

public interface ITitleCasingRulesUpdateService
{
    Task<TitleCasingRulesUpdateResult> UpdateAsync(
        string language,
        LanguageTitleCasingRulesUpdateRequest body,
        CancellationToken cancellationToken);
}
