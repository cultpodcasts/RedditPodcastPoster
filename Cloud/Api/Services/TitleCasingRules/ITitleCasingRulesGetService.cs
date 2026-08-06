using Api.Models;

namespace Api.Services.TitleCasingRules;

public interface ITitleCasingRulesGetService
{
    Task<TitleCasingRulesListGetResult> GetAllAsync(CancellationToken cancellationToken);

    Task<TitleCasingRulesGetResult> GetAsync(string language, CancellationToken cancellationToken);
}
