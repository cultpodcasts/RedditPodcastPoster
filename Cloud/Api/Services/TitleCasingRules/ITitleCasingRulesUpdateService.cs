using Api.Models;

namespace Api.Services.TitleCasingRules;

public interface ITitleCasingRulesUpdateService
{
    Task<TitleCasingRulesUpdateResult> AddLowerCaseTermAsync(
        string language,
        TitleCasingRulesAddLowerCaseTermRequest body,
        CancellationToken cancellationToken);

    Task<TitleCasingRulesUpdateResult> DeleteLowerCaseTermAsync(
        string language,
        string term,
        CancellationToken cancellationToken);

    Task<TitleCasingRulesUpdateResult> UpsertKnownTermAsync(
        string language,
        KnownTermUpdate body,
        CancellationToken cancellationToken);

    Task<TitleCasingRulesUpdateResult> DeleteKnownTermAsync(
        string language,
        string literal,
        CancellationToken cancellationToken);

    Task<TitleCasingRulesUpdateResult> AddIgnoredSubjectAsync(
        string language,
        TitleCasingRulesAddLowerCaseTermRequest body,
        CancellationToken cancellationToken);

    Task<TitleCasingRulesUpdateResult> DeleteIgnoredSubjectAsync(
        string language,
        string term,
        CancellationToken cancellationToken);
}
