using Api.Models;

namespace Api.Services.TitleCasingRules;

public interface ITitleCasingRulesGetService
{
    Task<TitleCasingRulesGetResult> GetAsync(string language, CancellationToken cancellationToken);
}
